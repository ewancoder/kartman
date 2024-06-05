﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KartMan.Api;

public sealed class WeatherStore : IWeatherStore
{
    private readonly NpgsqlDataSource _db;

    public WeatherStore(IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings__DataConnectionString"];
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _db = builder.Build();
    }

    /// <summary>
    /// Gets the latest weather available that was recorded BEFORE the given time,
    /// Otherwise returns null.
    /// </summary>
    public async ValueTask<WeatherData?> GetWeatherDataForAsync(DateTime time)
    {
        using var connection = await _db.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(@"
SELECT id, json_data
FROM weather_history
WHERE recorded_at < @time
ORDER BY recorded_at DESC
LIMIT 1", connection);
        command.Parameters.AddWithValue("time", time);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var data = JsonSerializer.Deserialize<WeatherData>(reader.GetString(1))
                ?? throw new InvalidOperationException("Could not deserialize weather.");

            data.Id = reader.GetInt64(0);

            return data;
        }

        return null;
    }

    public async ValueTask StoreAsync(WeatherData data)
    {
        try
        {
            using var connection = await _db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO weather_history (recorded_at, air_temp, humidity, precipitation, cloud, json_data)
VALUES (@recorded_at, @air_temp, @humidity, @precipitation, @cloud, @json_data);", connection);
            cmd.Parameters.AddWithValue("recorded_at", data.TimestampUtc);
            cmd.Parameters.AddWithValue("air_temp", data.TempC);
            cmd.Parameters.AddWithValue("humidity", data.Humidity);
            cmd.Parameters.AddWithValue("precipitation", data.PrecipitationMm);
            cmd.Parameters.AddWithValue("cloud", data.Cloud);
            var jsonData = cmd.Parameters.Add("json_data", NpgsqlTypes.NpgsqlDbType.Json);
            jsonData.Value = JsonSerializer.Serialize(data);

            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // TODO: Log this.
            throw;
        }
    }
}
