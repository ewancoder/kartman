using Npgsql;

namespace KartMan.Api.Weather;

public interface IWeatherStore
{
    /// <summary>
    /// Gets the latest weather available that was recorded BEFORE the given time,
    /// Otherwise returns null.
    /// </summary>
    ValueTask<WeatherData?> GetLastWeatherBeforeAsync(DateTime time);
    ValueTask StoreAsync(WeatherData data);
}

public sealed class WeatherStore : IWeatherStore
{
    private readonly NpgsqlDataSource _db;
    private readonly ILogger<WeatherStore> _logger;

    public WeatherStore(
        ILogger<WeatherStore> logger,
        NpgsqlDataSource db)
    {
        _logger = logger;
        _db = db;
    }

    public async ValueTask<WeatherData?> GetLastWeatherBeforeAsync(DateTime time)
    {
        using var _ = _logger.AddScoped("Time", time)
            .BeginScope();

        try
        {
            await using var connection = await _db.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, json_data
                FROM weather_history
                WHERE recorded_at < @time
                ORDER BY recorded_at DESC
                LIMIT 1;
            """;
            command.Parameters.AddWithValue("time", time);

            _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var data = JsonSerializer.Deserialize<WeatherData>(reader.GetString(1))
                    ?? throw new InvalidOperationException("Could not deserialize weather.");

                data.Id = reader.GetInt64(0);

                _logger.LogDebug("Got the weather from the database: {@Weather}", data);

                return data;
            }

            _logger.LogDebug("Did not find closest match of a weather in the database, returning null");
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get the weather from the database");
            return null;
        }
    }

    public async ValueTask StoreAsync(WeatherData data)
    {
        _logger.LogDebug("Storing weather data {@WeatherData} into the database", data);

        try
        {
            await using var connection = await _db.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO weather_history (recorded_at, air_temp, humidity, precipitation, cloud, json_data)
                VALUES (@recorded_at, @air_temp, @humidity, @precipitation, @cloud, @json_data);
            """;
            command.Parameters.AddWithValue("recorded_at", data.TimestampUtc);
            command.Parameters.AddWithValue("air_temp", data.TempC);
            command.Parameters.AddWithValue("humidity", data.Humidity);
            command.Parameters.AddWithValue("precipitation", data.PrecipitationMm);
            command.Parameters.AddWithValue("cloud", data.Cloud);
            var jsonData = command.Parameters.Add("json_data", NpgsqlTypes.NpgsqlDbType.Json);
            jsonData.Value = JsonSerializer.Serialize(data);

            _logger.LogDebug("Executing SQL command: {Command}", command.CommandText);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to store the weather into the database");
            throw;
        }
    }
}
