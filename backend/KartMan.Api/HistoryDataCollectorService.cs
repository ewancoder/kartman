﻿using System;
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
    decimal? gap)
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
    private readonly HashSet<ComparisonEntry> _cache = new HashSet<ComparisonEntry>();
    private readonly HashSet<string> _savedSessions = new HashSet<string>();
    private readonly IWeatherStore _weatherStore;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private readonly NpgsqlDataSource _db;

    public HistoryDataRepository(
        IConfiguration configuration,
        IWeatherStore weatherStore)
    {
        _weatherStore = weatherStore;
        var connectionString = configuration["ConnectionStrings__DataConnectionString"];
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _db = builder.Build();
    }

    public async Task<IEnumerable<LapEntry>> GetHistoryForDayAsync(DateOnly day)
    {
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
                decimal? gap = (await reader.IsDBNullAsync(7))
                    ? null
                    : reader.GetDecimal(7);

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

    public async Task SaveLapAsync(DateOnly day, LapEntry entry)
    {
        if (_cache.Contains(entry.ToComparisonEntry()))
            return; // Saves entry only if it hasn't been saved yet.

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
        command.Parameters.AddWithValue("gap", entry.gap.HasValue ? entry.gap.Value : DBNull.Value);
        command.Parameters.AddWithValue("weather_id", DBNull.Value);

        await command.ExecuteNonQueryAsync();
        _cache.Add(entry.ToComparisonEntry()); // May be a slight racing condition. Consider locking around these.
    }

    public async Task CreateOrGetSessionAsync(DateOnly day, LapEntry entry)
    {
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

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        using var connection = await _db.OpenConnectionAsync();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
            SELECT s.id, w.weather, w.sky, w.wind, w.air_temp, w.track_temp, w.track_temp_approximation, s.track_config
            FROM session s
            JOIN weather w ON w.id = s.weather_id
            WHERE id = @id
        ";
        command.Parameters.AddWithValue("id", sessionId);

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

        return null;
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
    private const int StartTimeHourUtc = 0; // 5 GMT, 9 AM.
    private const int EndTimeHourUtc = 24; // 19 PMT, 11 PM.
    private DateTime _lastTelemetryRecordedAtUtc;
    private string? _lastSession;
    private bool _dayEnded = false;

    public HistoryDataCollectorService(
        IHttpClientFactory httpClientFactory,
        HistoryDataRepository repository)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gatheringData = Task.Run(async () =>
        {
            while (true)
            {
                if ((DateTime.UtcNow.Hour < StartTimeHourUtc || DateTime.UtcNow.Hour >= EndTimeHourUtc)
                    && DateTime.UtcNow - _lastTelemetryRecordedAtUtc > TimeSpan.FromHours(1.5))
                {
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

        Console.WriteLine("Started gathering history data.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopped gathering history data.");
        _isRunning = false;
        return Task.CompletedTask;
    }

    private async Task GatherDataAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            //var response = await client.GetAsync("https://kart-timer.com/drivers/ajax.php?p=livescreen&track=110&target=updaterace");
            var response = await client.GetAsync("https://kart-timer.com/drivers/ajax.php?p=livescreen&track=90&target=updaterace");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var hash = Encoding.UTF8.GetString(MD5.HashData(Encoding.UTF8.GetBytes(content)));
            if (_previousHash == hash)
                return;
            _previousHash = hash;
            _lastTelemetryRecordedAtUtc = DateTime.UtcNow;

            var rawJson = JsonSerializer.Deserialize<RawJson>(content)
                ?? throw new InvalidOperationException("Could not deserialize result from karting api.");
            // TODO: Send signalr update here to update the web page, we got new data.

            if (_dayEnded && _lastSession == rawJson.headinfo.number)
                return; // TODO: If there were ZERO sessions for the whole day - this will cause first session of the next day to be lost. Try to fix this.

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
                        Convert.ToInt32(x[0]?.ToString()), string.IsNullOrEmpty(x[7]?.ToString()) ? null : Convert.ToDecimal(x[7].ToString())); // TODO: Figure out last 2 values. Debug.
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"{DateTime.UtcNow} Error when trying to parse data for lap entry: {exception.Message}, {exception.StackTrace}, {exception.InnerException?.Message}");
                    return null;
                }
            }).Where(x => x != null).ToList();

            foreach (var entry in entries)
            {
                await _repository.SaveLapAsync(DateOnly.FromDateTime(DateTime.UtcNow), entry!);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"{DateTime.UtcNow} Error when trying to gather data: {exception.Message}, {exception.StackTrace}, {exception.InnerException?.Message}");
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
