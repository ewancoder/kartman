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

    public async ValueTask StoreAsync(WeatherData data)
    {
        try
        {
        var connection = await _db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO weather_history (recorded_at, air_temp, humidity, precipitation, clouds, json_data)
VALUES (@recorded_at, @air_temp, @humidity, @precipitation, @clouds, @json_data);", connection);
        cmd.Parameters.AddWithValue("recorded_at", data.TimestampUtc);
        cmd.Parameters.AddWithValue("air_temp", data.TempC);
        cmd.Parameters.AddWithValue("humidity", data.Humidity);
        cmd.Parameters.AddWithValue("precipitation", data.PrecipitationMm);
        cmd.Parameters.AddWithValue("clouds", data.Cloud);
        var jsonData = cmd.Parameters.Add("json_data", NpgsqlTypes.NpgsqlDbType.Json);
        jsonData.Value = JsonSerializer.Serialize(data);

            var x = await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
        }
    }
}
