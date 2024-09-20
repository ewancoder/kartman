using KartMan.Api.Weather;
using Npgsql;

namespace KartMan.Api;

public sealed class HistoryDataRepository
{
    private readonly ILogger<HistoryDataRepository> _logger;
    private readonly HashSet<string> _savedSessions = [];
    private readonly IWeatherStore _weatherStore;
    private readonly NpgsqlDataSource _db;

    public HistoryDataRepository(
        ILogger<HistoryDataRepository> logger,
        IWeatherStore weatherStore,
        NpgsqlDataSource db)
    {
        _logger = logger;
        _weatherStore = weatherStore;
        _db = db;
    }

    public async ValueTask<IEnumerable<SessionInfo>> GetSessionInfosForDay(DateOnly day)
    {
        using var _ = _logger.AddScoped("Day", day).BeginScope();
        try
        {
            _logger.LogDebug("Getting session infos for day {Day}", day);

            await using var connection = await _db.OpenConnectionAsync();
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT s.id, s.recorded_at, s.session, coalesce(w.air_temp, wh.air_temp)
                FROM session s
                JOIN weather w ON s.weather_id = w.id
                JOIN weather_history wh ON w.weather_history_id = wh.id
                WHERE s.day = @day
                ORDER BY coalesce(s.updated_at, s.recorded_at) DESC
            """;
            command.Parameters.AddWithValue("day", day.DayNumber);

            _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
            var list = new List<SessionInfo>();
            await using var reader = await command.ExecuteReaderAsync();
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

            return list;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get day sessions");
            throw;
        }
    }

    public async ValueTask<DateTime> GetFirstRecordedTimeAsync()
    {
        await using var connection = await _db.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT d.recorded_at
            FROM lap_data d
            ORDER BY d.recorded_at
            LIMIT 1
        """;

        _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return reader.GetDateTime(0);

        throw new InvalidOperationException("Could not get first recorded time");
    }

    public async ValueTask<long> GetTotalLapsDrivenAsync()
    {
        await using var connection = await _db.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT d.id
            FROM lap_data d
            ORDER BY d.id DESC
            LIMIT 1
        """;

        _logger.LogDebug("Executing SQL command {Command}", command.CommandText);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return reader.GetInt64(0);

        throw new InvalidOperationException("Could not get total laps driven");
    }

    public async ValueTask<IEnumerable<KartDrive>> GetHistoryForSessionAsync(string sessionId)
    {
        using var _ = _logger.AddScoped("SessionId", sessionId).BeginScope();
        try
        {
            _logger.LogDebug("Getting history for session {SessionId}", sessionId);

            await using var connection = await _db.OpenConnectionAsync();
            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT d.kart, d.lap, d.laptime, d.id, d.invalid_lap
                FROM lap_data d
                WHERE d.session_id = @sessionId
            """;
            command.Parameters.AddWithValue("sessionId", sessionId);

            _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
            var list = new List<KartDrive>();
            await using var reader = await command.ExecuteReaderAsync();
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
        await using var connection = await _db.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE lap_data l
            SET invalid_lap = @isInvalid
            WHERE l.id = @lapId
        """;
        command.Parameters.AddWithValue("lapId", lapId);
        command.Parameters.AddWithValue("isInvalid", isInvalid);

        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveLapAsync(DateOnly day, LapEntry entry)
    {
        var _ = _logger.AddScoped("Day", day);
        try
        {
            _logger.LogDebug("Saving lap data: {Day}, {@Entry}", day, entry);

            var sessionId = entry.GetSessionIdentifier();
            if (!_savedSessions.Contains(sessionId)) // Performance optimization. REWRITE to use SQL instead of state in this repository.
            {
                _logger.LogDebug("Session has not been created yet for this entry. Creating the session {SessionId}.", sessionId);
                await CreateOrGetSessionAsync(day, entry);
                _savedSessions.Add(sessionId);
            }

            await using var connection = await _db.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO lap_data (session_id, recorded_at, kart, lap, laptime, position, gap, weather_id, invalid_lap)
                VALUES (@session_id, @recorded_at, @kart, @lap, @laptime, @position, @gap, @weather_id, @invalid_lap)
                ON CONFLICT (session_id, kart, lap) DO UPDATE SET laptime=@laptime, position=@position, gap=@gap, recorded_at=@recorded_at, invalid_lap=@invalid_lap;";
            """;
            command.Parameters.AddWithValue("session_id", entry.GetSessionIdentifier());
            command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
            command.Parameters.AddWithValue("kart", entry.kart);
            command.Parameters.AddWithValue("lap", entry.lap);
            command.Parameters.AddWithValue("laptime", entry.time);
            command.Parameters.AddWithValue("position", entry.position);
            command.Parameters.AddWithValue("gap", entry.gap != null ? entry.gap : DBNull.Value);
            command.Parameters.AddWithValue("weather_id", DBNull.Value);
            command.Parameters.AddWithValue("invalid_lap", entry.time <= 20 || entry.time >= 90);

            _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to store laptime data");
            throw;
        }
    }

    private async Task CreateOrGetSessionAsync(DateOnly day, LapEntry entry)
    {
        try
        {
            _logger.LogInformation("Creating session {SessionId}", entry.GetSessionIdentifier());
            await using var connection = await _db.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var weather = await _weatherStore.GetLastWeatherBeforeAsync(entry.recordedAtUtc);

            static int GetWeather(WeatherData weather)
            {
                if (weather.PrecipitationMm == 0)
                    return 1; // Dry.

                if (weather.PrecipitationMm < 1)
                    return 2; // Damp.

                if (weather.PrecipitationMm < 5)
                    return 3; // Wet.

                return 4; // Extra wet.
            }

            static int GetSky(WeatherData weather)
            {
                if (weather.Cloud < 15)
                    return 1; // Clear.

                if (weather.Cloud < 70)
                    return 2; // Cloudy.

                return 3; // Overcast.
            }

            static int GetWind(WeatherData weather)
            {
                if (weather.WindKph < 10)
                    return 1; // No Wind.

                return 2; // Yes Wind.
            }

            // TODO: If this method is called after API restart, it will insert one more weather without updating it on the already existing session.
            long weatherId;
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = """
                    INSERT INTO weather (recorded_at, weather_history_id, air_temp, humidity, precipitation, cloud, weather, sky, wind)
                    VALUES (@recorded_at, @weather_history_id, @air_temp, @humidity, @precipitation, @cloud, @weather, @sky, @wind)
                    RETURNING id";
                """;
                command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
                command.Parameters.AddWithValue("weather_history_id", weather == null ? DBNull.Value : weather.Id);
                command.Parameters.AddWithValue("air_temp", weather == null ? DBNull.Value : weather.TempC);
                command.Parameters.AddWithValue("humidity", weather == null ? DBNull.Value : weather.Humidity);
                command.Parameters.AddWithValue("precipitation", weather == null ? DBNull.Value : weather.PrecipitationMm);
                command.Parameters.AddWithValue("cloud", weather == null ? DBNull.Value : weather.Cloud);
                command.Parameters.AddWithValue("weather", weather == null ? DBNull.Value : (int)GetWeather(weather));
                command.Parameters.AddWithValue("sky", weather == null ? DBNull.Value : (int)GetSky(weather));
                command.Parameters.AddWithValue("wind", weather == null ? DBNull.Value : (int)GetWind(weather));

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                var idObj = await command.ExecuteScalarAsync()
                    ?? throw new InvalidOperationException("Database insert returned null result.");
                weatherId = Convert.ToInt64(idObj.ToString() ?? throw new InvalidOperationException("Database insert returned null result"));
            }

            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;

                command.CommandText = """
                    WITH new_or_existing AS (
                        INSERT INTO session (id, recorded_at, day, session, total_length, weather_id, track_config, updated_at)
                        VALUES (@id, @recorded_at, @day, @session, @total_length, @weather_id, @track_config, @recorded_at)
                        ON CONFLICT (id) DO UPDATE SET updated_at = NOW()
                        RETURNING id
                    ) SELECT * FROM new_or_existing;";
                """;

                command.Parameters.AddWithValue("id", entry.GetSessionIdentifier());
                command.Parameters.AddWithValue("recorded_at", entry.recordedAtUtc);
                command.Parameters.AddWithValue("day", day.DayNumber);
                command.Parameters.AddWithValue("session", entry.session);
                command.Parameters.AddWithValue("total_length", entry.totalLength);
                command.Parameters.AddWithValue("weather_id", weatherId);
                command.Parameters.AddWithValue("track_config", DBNull.Value);

                _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
                await command.ExecuteNonQueryAsync();
            }

            _logger.LogDebug("Committing transaction");
            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create session & weather data");
            throw;
        }
    }
}
