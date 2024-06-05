using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace KartMan.Api;

public sealed record RawWeatherData(
    RawCurrent current);
public sealed record RawCondition(
    int code,
    string text);
public sealed record RawCurrent(
    decimal temp_c,
    int is_day,
    RawCondition condition,
    decimal wind_kph,
    decimal wind_degree,
    decimal pressure_mb,
    decimal precip_mm,
    decimal humidity,
    decimal cloud,
    decimal feelslike_c,
    decimal dewpoint_c);

public sealed record WeatherData(
    DateTime TimestampUtc,
    decimal TempC,
    bool IsDay,
    int ConditionCode,
    string ConditionText,
    decimal WindKph,
    decimal WindDegree,
    decimal PressureMb,
    decimal PrecipitationMm,
    decimal Humidity,
    decimal Cloud,
    decimal FeelsLikeC,
    decimal DewPointC)
{
    public WeatherComparison ToComparison()
    {
        return new(
            TempC, IsDay, ConditionCode, WindKph, WindDegree, PressureMb, PrecipitationMm, Humidity, Cloud, FeelsLikeC, DewPointC);
    }
}

public sealed record WeatherComparison(
    decimal TempC,
    bool IsDay,
    int ConditionCode,
    decimal WindKph,
    decimal WindDegree,
    decimal PressureMb,
    decimal PrecipitationMb,
    decimal Humidity,
    decimal Cloud,
    decimal FeelsLikeC,
    decimal DewPointC);

public interface IWeatherRetriever
{
    /// <summary>
    /// Returns null if upstread service call was unsuccessful.
    /// </summary>
    ValueTask<WeatherData?> GetWeatherAsync();
}
public sealed class WeatherRetriever : IWeatherRetriever
{
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherRetriever(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["WeatherApiKey"]
            ?? throw new InvalidOperationException("Could not get WeatherApiKey.");
    }

    public async ValueTask<WeatherData?> GetWeatherAsync()
    {
        using var client = _httpClientFactory.CreateClient();

        try
        {
            var response = await client.GetAsync($"https://api.weatherapi.com/v1/current.json?key={_apiKey}&q=Batumi&aqi=no");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var raw = JsonSerializer.Deserialize<RawWeatherData>(content)
                ?? throw new InvalidOperationException("Could not get the weather.");

            return new WeatherData(
                DateTime.UtcNow,
                raw.current.temp_c,
                raw.current.is_day == 1,
                raw.current.condition.code,
                raw.current.condition.text,
                raw.current.wind_kph,
                raw.current.wind_degree,
                raw.current.pressure_mb,
                raw.current.precip_mm,
                raw.current.humidity,
                raw.current.cloud,
                raw.current.feelslike_c,
                raw.current.dewpoint_c);
        }
        catch (Exception exception)
        {
            // TODO: Log this.
            return null;
        }
    }
}

public interface IWeatherStore
{
    ValueTask StoreAsync(WeatherData data);
}

public sealed class WeatherGatherer
{
    private readonly IWeatherRetriever _weatherRetriever;
    private readonly IWeatherStore _weatherStore;
    private readonly Task _gathering;
    private WeatherData? _lastData;

    public WeatherGatherer(
        IWeatherRetriever weatherRetriever,
        IWeatherStore weatherStore)
    {
        _weatherRetriever = weatherRetriever;
        _weatherStore = weatherStore;
        _gathering = GatherAsync();
    }

    private async Task GatherAsync()
    {
        while (true)
        {
            try
            {
                await GatherWeatherAsync();
            }
            catch
            {
                // TODO: Log this.
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }

    private async Task GatherWeatherAsync()
    {
        var data = await _weatherRetriever.GetWeatherAsync();
        if (data == null)
            return; // Could not get the weather from weather api.

        if (_lastData == null || _lastData.ToComparison() != data.ToComparison())
        {
            await _weatherStore.StoreAsync(data);
            _lastData = data;
        }
    }
}
