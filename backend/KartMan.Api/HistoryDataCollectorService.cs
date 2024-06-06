using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KartMan.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KartMan.Host;

public sealed record ShortEntry(
    int lap, decimal time);

public sealed record LapEntry(
    DateTime recordedAtUtc,
    int session,
    string totalLength,
    string kart,
    int lap,
    decimal time,
    int position,
    string? gap)
{
    public ComparisonEntry ToComparisonEntry() => new(DateOnly.FromDateTime(recordedAtUtc), session, kart, lap);

    public string GetSessionIdentifier()
    {
        return $"{DateOnly.FromDateTime(recordedAtUtc).DayNumber}-{session}";
    }

    public string SessionId => GetSessionIdentifier();
}

public sealed record RawJson(
    RawHeadInfo headinfo,
    object[][] results);

public sealed record RawHeadInfo(
    string number,
    string len);

public sealed class HistoryDataRepository
{
    private readonly ILogger<HistoryDataRepository> _logger;
    private readonly HashSet<ComparisonEntry> _cache = new HashSet<ComparisonEntry>();
    private readonly HashSet<string> _savedSessions = new HashSet<string>();
    private readonly IWeatherStore _weatherStore;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly NpgsqlDataSource _db;

    public HistoryDataRepository(
        ILogger<HistoryDataRepository> logger,
        IConfiguration configuration,
        IWeatherStore weatherStore)
    {
        _logger = logger;
        _weatherStore = weatherStore;
        var connectionString = configuration["DbConnectionString"];
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _db = builder.Build();
    }

    public async Task<IEnumerable<LapEntry>> GetHistoryForDayAsync(DateOnly day)
    {
        using var _ = _logger.AddScoped("Day", day).BeginScope();
        try
        {
            _logger.LogDebug("Getting history for day {Day}.", day);

            using var connection = await _db.OpenConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT d.recorded_at, s.session, s.total_length, d.kart, d.lap, d.laptime, d.position, d.gap
                FROM lap_data d
                JOIN session s ON d.session_id = s.id
                WHERE s.day = @day
            ";
            command.Parameters.AddWithValue("day", day.DayNumber);

            _logger.LogDebug("Executing SQL command {Command}.", command.CommandText);
            var list = new List<LapEntry>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var recordedAtUtc = reader.GetDateTime(0);
                    var session = reader.GetInt32(1);
                    var totalLength = reader.GetString(2);
                    var kart = reader.GetString(3);
                    var lap = reader.GetInt32(4);
                    var time = reader.GetDecimal(5);
                    var position = reader.GetInt32(6);
                    var gap = (await reader.IsDBNullAsync(7))
                        ? null
                        : reader.GetString(7);

                    list.Add(new LapEntry(
                        recordedAtUtc,
                        session,
                        totalLength,
                        kart,
                        lap,
                        time,
                        position,
                        gap));
                }
            }

            return list;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get day history.");
            throw;
        }
    }

    public async Task SaveLapAsync(DateOnly day, LapEntry entry)
    {
        var _ = _logger.AddScoped("Day", day);
        try
        {
            _logger.LogDebug("Saving lap data: {Day}, {@Entry}.", day, entry);

            if (_cache.Contains(entry.ToComparisonEntry()))
            {
                _logger.LogDebug("Already saved this entry, skipping.");
                return; // Saves entry only if it hasn't been saved yet.
            }

            // If app is restarted - it may try record multiple entries twice.
            // Or multiple sessions twice.
            // Restart only during nighttime or refactor this.

            var sessionId = entry.GetSessionIdentifier();
            if (!_savedSessions.Contains(sessionId))
            {
                // Session has not been created yet for this entry.
                // Creating.
                await _lock.WaitAsync();
                try
                {
                    if (!_savedSessions.Contains(sessionId))
                    {
                        _logger.LogDebug("Session has not been created yet for this entry. Creating the session {SessionId}.", sessionId);
                        await CreateOrGetSessionAsync(day, entry);
                        _savedSessions.Add(sessionId);
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }

            using var connection = await _db.OpenConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
INSERT INTO lap_data (session_id, recorded_at, kart, lap, laptime, position, gap, weather_id)
VALUES (@session_id, @recorded_at, @kart, @lap, @laptime, @position, @gap, @weather_id)
ON CONFLICT (session_id, kart, lap) DO UPDATE SET laptime=@laptime, position=@position, gap=@gap, recorded_at=@recorded_at;";
            command.Parameters.AddWithValue("session_id", entry.GetSessionIdentifier());
            command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
            command.Parameters.AddWithValue("kart", entry.kart);
            command.Parameters.AddWithValue("lap", entry.lap);
            command.Parameters.AddWithValue("laptime", entry.time);
            command.Parameters.AddWithValue("position", entry.position);
            command.Parameters.AddWithValue("gap", entry.gap != null ? entry.gap : DBNull.Value);
            command.Parameters.AddWithValue("weather_id", DBNull.Value);

            _logger.LogDebug("Executing SQL command {Command}.", command.CommandText);
            await command.ExecuteNonQueryAsync();
            _cache.Add(entry.ToComparisonEntry()); // May be a slight racing condition. Consider locking around these.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to store laptime data.");
            throw;
        }
    }

    public async Task CreateOrGetSessionAsync(DateOnly day, LapEntry entry)
    {
        try
        {
            _logger.LogInformation("Creating session {SessionId}.", entry.GetSessionIdentifier());
            using var connection = await _db.OpenConnectionAsync();
            using var transaction = await connection.BeginTransactionAsync();
            var weather = await _weatherStore.GetWeatherDataForAsync(entry.recordedAtUtc);

            static Weather GetWeather(WeatherData weather)
            {
                if (weather.PrecipitationMm == 0)
                    return Weather.Dry;

                if (weather.PrecipitationMm < 1)
                    return Weather.Damp;

                if (weather.PrecipitationMm < 5)
                    return Weather.Wet;

                return Weather.ExtraWet;
            }

            static Sky GetSky(WeatherData weather)
            {
                if (weather.Cloud < 15)
                    return Sky.Clear;

                if (weather.Cloud < 70)
                    return Sky.Cloudy;

                return Sky.Overcast;
            }

            static Wind GetWind(WeatherData weather)
            {
                if (weather.WindKph < 10)
                    return Wind.NoWind;

                return Wind.Yes;
            }

            // TODO: Do not create duplicate weather entries, get existing for this session (after app restart).
            long weatherId;
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
    INSERT INTO weather (recorded_at, weather_history_id, air_temp, humidity, precipitation, cloud, weather, sky, wind)
    VALUES (@recorded_at, @weather_history_id, @air_temp, @humidity, @precipitation, @cloud, @weather, @sky, @wind)
    RETURNING id";
                command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
                command.Parameters.AddWithValue("weather_history_id", weather == null ? DBNull.Value : weather.Id);
                command.Parameters.AddWithValue("air_temp", weather == null ? DBNull.Value : weather.TempC);
                command.Parameters.AddWithValue("humidity", weather == null ? DBNull.Value : weather.Humidity);
                command.Parameters.AddWithValue("precipitation", weather == null ? DBNull.Value : weather.PrecipitationMm);
                command.Parameters.AddWithValue("cloud", weather == null ? DBNull.Value : weather.Cloud);
                command.Parameters.AddWithValue("weather", weather == null ? DBNull.Value : (int)GetWeather(weather));
                command.Parameters.AddWithValue("sky", weather == null ? DBNull.Value : (int)GetSky(weather));
                command.Parameters.AddWithValue("wind", weather == null ? DBNull.Value : (int)GetWind(weather));

                _logger.LogDebug("Executing SQL command {Command}.", command.CommandText);
                var idObj = await command.ExecuteScalarAsync()
                    ?? throw new InvalidOperationException("Database insert returned null result.");
                weatherId = Convert.ToInt64(idObj.ToString() ?? throw new InvalidOperationException("Database insert returned null result"));
            }

            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = @"
    WITH new_or_existing AS (
        INSERT INTO session (id, recorded_at, day, session, total_length, weather_id, track_config)
        VALUES (@id, @recorded_at, @day, @session, @total_length, @weather_id, @track_config)
        ON CONFLICT (id) DO UPDATE SET id = @id
        RETURNING id
    ) SELECT * FROM new_or_existing;";

                command.Parameters.AddWithValue("id", entry.GetSessionIdentifier());
                command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
                command.Parameters.AddWithValue("day", day.DayNumber);
                command.Parameters.AddWithValue("session", entry.session);
                command.Parameters.AddWithValue("total_length", entry.totalLength);
                command.Parameters.AddWithValue("weather_id", weatherId);
                command.Parameters.AddWithValue("track_config", DBNull.Value);

                _logger.LogDebug("Executing SQL command {Command}.", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogDebug("Committing transaction.");
            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create session & weather data.");
            throw;
        }
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        using var _ = _logger.AddScoped("SessionId", sessionId).BeginScope();
        try
        {
            using var connection = await _db.OpenConnectionAsync();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT s.id, w.weather, w.sky, w.wind, w.air_temp, w.track_temp, w.track_temp_approximation, s.track_config
                FROM session s
                JOIN weather w ON w.id = s.weather_id
                WHERE s.id = @id
            ";
            command.Parameters.AddWithValue("id", sessionId);

            _logger.LogDebug("Executing SQL command {Command}.", command.CommandText);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                Weather? weather = reader.IsDBNull(1) ? null : (Weather)reader.GetInt32(1);
                Sky? sky = reader.IsDBNull(2) ? null : (Sky)reader.GetInt32(2);
                Wind? wind = reader.IsDBNull(3) ? null : (Wind)reader.GetInt32(3);
                decimal? airTemp = reader.IsDBNull(4) ? null : reader.GetDecimal(4);
                decimal? trackTemp = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
                TrackTemp? trackTempApproximation = reader.IsDBNull(6) ? null : (TrackTemp)reader.GetInt32(6);
                TrackConfig? trackConfig = reader.IsDBNull(7) ? null : (TrackConfig)reader.GetInt32(7);

                return new SessionInfo(weather, sky, wind, airTemp, trackTemp,
                    trackTempApproximation, trackConfig);
            }

            _logger.LogDebug("Did not find session information, returning null.");
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get session info.");
            throw;
        }
    }
}

public sealed record ComparisonEntry(DateOnly day, int session, string kart, int lap);
public sealed class HistoryDataCollectorService : IHostedService
{
    private bool _isRunning = true;
    private Task? _gatheringData;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HistoryDataRepository _repository;
    private string? _previousHash;
    private const int StartTimeHourUtc = 5; // 5 GMT, 9 AM.
    private const int EndTimeHourUtc = 19; // 19 PMT, 11 PM.
    private DateTime _lastTelemetryRecordedAtUtc;
    private string? _lastSession;
    private bool _dayEnded = false;
    private readonly ILogger<HistoryDataCollectorService> _logger;

    public HistoryDataCollectorService(
        IHttpClientFactory httpClientFactory,
        HistoryDataRepository repository,
        ILogger<HistoryDataCollectorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gatheringData = Task.Run(async () =>
        {
            while (true)
            {
                using var _ = _logger.AddScoped("KartingTrace", Guid.NewGuid().ToString())
                    .BeginScope();

                if ((DateTime.UtcNow.Hour < StartTimeHourUtc || DateTime.UtcNow.Hour >= EndTimeHourUtc)
                    && DateTime.UtcNow - _lastTelemetryRecordedAtUtc > TimeSpan.FromHours(1.5))
                {
                    _logger.LogTrace("Skipping logging of karting data because it's not a working time of day. Waiting for 5 minutes before next check.");
                    _dayEnded = true;
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    continue;
                }

                if (!_isRunning)
                    return;

                await GatherDataAsync();

                await Task.Delay(3000);
            }
        });

        _logger.LogInformation("Started gathering history data.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopped gathering history data.");
        _isRunning = false;
        return Task.CompletedTask;
    }

    private async Task GatherDataAsync()
    {
        try
        {
            _logger.LogDebug("Logging karting data.");
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://kart-timer.com/drivers/ajax.php?p=livescreen&track=110&target=updaterace");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var hash = Encoding.UTF8.GetString(MD5.HashData(Encoding.UTF8.GetBytes(content)));
            if (_previousHash == hash)
            {
                _logger.LogTrace("Karting data didn't change since last time, not logging it.");
                return;
            }
            _previousHash = hash;
            _lastTelemetryRecordedAtUtc = DateTime.UtcNow;

            var rawJson = JsonSerializer.Deserialize<RawJson>(content)
                ?? throw new InvalidOperationException("Could not deserialize result from karting api.");
            // TODO: Send signalr update here to update the web page, we got new data.

            if (_dayEnded && _lastSession == rawJson.headinfo.number)
            {
                _logger.LogDebug("Day has ended and it is the last session. Skipping.");
                return; // TODO: If there were ZERO sessions for the whole day - this will cause first session of the next day to be lost. Try to fix this.
            }

            if (_dayEnded) _dayEnded = false;

            _lastSession = rawJson.headinfo.number;

            var entries = rawJson.results.Select(x =>
            {
                var time = x[6]?.ToString();
                if (string.IsNullOrEmpty(time) || !decimal.TryParse(time, out var _)) return null;

                try
                {
                    // TODO: Also save NAME of person - for other kartings it works.
                    return new LapEntry(
                        DateTime.UtcNow,
                        Convert.ToInt32(rawJson.headinfo.number),
                        rawJson.headinfo.len,
                        x[2].ToString()!,
                        Convert.ToInt32(x[3].ToString()),
                        Convert.ToDecimal(time),
                        Convert.ToInt32(x[0]?.ToString()), x[7]?.ToString()); // TODO: Figure out last 2 values. Debug.
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Error when trying to parse data for lap entry.");
                    return null;
                }
            }).Where(x => x != null).ToList();

            _logger.LogInformation("Saving karting entries to the database.");
            foreach (var entry in entries)
            {
                await _repository.SaveLapAsync(DateOnly.FromDateTime(DateTime.UtcNow), entry!);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to gather karting data on the interval.");
        }
    }
}

public enum Weather
{
    Dry = 1,
    Damp,
    Wet,
    ExtraWet
}

public enum Sky
{
    Clear = 1,
    Cloudy,
    Overcast
}

public enum Wind
{
    NoWind = 1,
    Yes = 2
}

public enum TrackTemp
{
    Cold = 1,
    Cool,
    Warm,
    Hot
}

public enum TrackConfig
{
    Short = 1,
    Long,
    ShortReverse,
    LongReverse
}

public record SessionInfo(
    Weather? Weather,
    Sky? Sky,
    Wind? Wind,
    decimal? AirTempC,
    decimal? TrackTempC,
    TrackTemp? TrackTempApproximation,
    TrackConfig? TrackConfig)
{
    public bool IsValid => Weather != null || Sky != null || Wind != null || AirTempC != null || TrackTempC != null
        || TrackTempApproximation != null || TrackConfig != null;
}
