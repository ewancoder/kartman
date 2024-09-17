using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KartMan.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KartMan.Host;

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
        builder.ConnectionStringBuilder.IncludeErrorDetail = true;
        _db = builder.Build();
    }

    // TODO: Try IAsyncEnumerable.
    public async ValueTask<IEnumerable<SessionInfoNg>> GetSessionInfosForDay(DateOnly day)
    {
        using var _ = _logger.AddScoped("Day", day).BeginScope();
        try
        {
            _logger.LogDebug("Getting session infos for day {Day}", day);

            using var connection = await _db.OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT s.id, s.recorded_at, s.session, coalesce(w.air_temp, wh.air_temp)
                FROM session s
                JOIN weather w ON s.weather_id = w.id
                JOIN weather_history wh ON w.weather_history_id = wh.id
                WHERE s.day = @day
                ORDER BY s.recorded_at DESC
            """;
            command.Parameters.AddWithValue("day", day.DayNumber);

            _logger.LogDebug("Executing SQL command {Command}", command.CommandText);
            var list = new List<SessionInfoNg>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var sessionId = reader.GetString(0);
                    var recordedAt = reader.GetDateTime(1);
                    var session = reader.GetInt32(2);
                    var airTemp = reader.GetDecimal(3);

                    list.Add(new(
                        sessionId,
                        $"Session {session}",
                        recordedAt,
                        new(airTemp)));
                }
            }

            return list;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get day sessions");
            throw;
        }
    }

    public async ValueTask<long> GetTotalLapsDrivenAsync()
    {
        using var connection = await _db.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT d.id
            FROM lap_data d
            ORDER BY d.id DESC
            LIMIT 1
        """;

        _logger.LogDebug("Executing SQL command {Command}", command.CommandText);
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt64(0);
        }

        throw new InvalidOperationException("Could not get total laps driven.");
    }

    // TODO: Try IAsyncEnumerable.
    public async ValueTask<IEnumerable<KartDriveNg>> GetHistoryForSessionAsync(string sessionId)
    {
        using var _ = _logger.AddScoped("SessionId", sessionId).BeginScope();
        try
        {
            _logger.LogDebug("Getting history for session {SessionId}", sessionId);

            using var connection = await _db.OpenConnectionAsync();
            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT d.kart, d.lap, d.laptime, d.id, d.invalid_lap
                FROM lap_data d
                WHERE d.session_id = @sessionId
            """;
            command.Parameters.AddWithValue("sessionId", sessionId);

            _logger.LogDebug("Executing SQL command {Command}", command.CommandText);
            var list = new List<KartDriveNg>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var kart = reader.GetString(0);
                    var lap = reader.GetInt32(1);
                    var time = reader.GetDecimal(2);
                    var lapId = reader.GetInt64(3);
                    var invalidLap = !await reader.IsDBNullAsync(4)
                        && reader.GetBoolean(4);

                    // TODO: Consider storing unique Kart IDs for each separate driving session.
                    list.Add(new(lapId, kart, lap, time, invalidLap));
                }
            }

            return list;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get session data");
            throw;
        }
    }

    public async ValueTask UpdateLapInvalidStatusAsync(long lapId, bool isInvalid)
    {
        using var connection = await _db.OpenConnectionAsync();
        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE lap_data l
            SET invalid_lap = @isInvalid
            WHERE l.id = @lapId
        """;
        command.Parameters.AddWithValue("lapId", lapId);
        command.Parameters.AddWithValue("isInvalid", isInvalid);

        await command.ExecuteNonQueryAsync();
    }



    public async Task<IEnumerable<LapEntry>> GetHistoryForDayAsync(DateOnly day)
    {
        using var _ = _logger.AddScoped("Day", day).BeginScope();
        try
        {
            _logger.LogDebug("Getting history for day {Day}.", day);

            using var connection = await _db.OpenConnectionAsync();

            using var command = connection.CreateCommand();
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

            using var command = connection.CreateCommand();
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
                using var command = connection.CreateCommand();
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
                using var command = connection.CreateCommand();
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

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
                SELECT s.id, w.weather, w.sky, w.wind, w.air_temp, w.track_temp, w.track_temp_info, s.track_config
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
                TrackTemp? trackTempApproximation = reader.IsDBNull(6) ? null : (TrackTemp)Convert.ToInt32(reader.GetString(6));
                TrackConfig? trackConfig = reader.IsDBNull(7) ? null : (TrackConfig)Convert.ToInt32(reader.GetString(7)); // TODO: If ever storing strings there - handle it here properly.

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

    public async Task UpdateSessionInfoAsync(string sessionId, SessionInfo info)
    {
        _logger.AddScoped("SessionId", sessionId).BeginScope();

        if (!info.IsValid)
        {
            _logger.LogError("Nothing to update, supplied request is empty. Skipping. {Session} {@Data}.", sessionId, info);
            return;
        }

        try
        {
            if (info.TrackConfig != null)
            {
                _logger.LogDebug("Updating track configuration.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE session SET track_config = @track_config WHERE id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("track_config", ((int)info.TrackConfig).ToString());

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.Sky != null)
            {
                _logger.LogDebug("Updating sky.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET sky = @sky FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("sky", (int)info.Sky);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.Weather != null)
            {
                _logger.LogDebug("Updating weather.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET weather = @weather FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("weather", (int)info.Weather);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.AirTempC != null)
            {
                _logger.LogDebug("Updating air temp.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET air_temp = @air_temp FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("air_temp", info.AirTempC);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.TrackTempC != null)
            {
                _logger.LogDebug("Updating track temp.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET track_temp = @track_temp FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("track_temp", info.TrackTempC);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.Wind != null)
            {
                _logger.LogDebug("Updating wind.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET wind = @wind FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("wind", (int)info.Wind);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            if (info.TrackTempApproximation != null)
            {
                _logger.LogDebug("Updating track temp info.");
                using var connection = await _db.OpenConnectionAsync();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE weather w SET track_temp_info = @track_temp_info FROM session s WHERE s.weather_id = w.id AND s.id = @id;";
                command.Parameters.AddWithValue("id", sessionId);
                command.Parameters.AddWithValue("track_temp_info", (int)info.TrackTempApproximation);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to update session data.");
            throw;
        }
    }
}
