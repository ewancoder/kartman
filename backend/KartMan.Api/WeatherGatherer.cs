using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    public long Id { get; set; } // Database identifier.
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
    private readonly ILogger<WeatherRetriever> _logger;
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherRetriever(
        ILogger<WeatherRetriever> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["WeatherApiKey"]
            ?? throw new InvalidOperationException("Could not get WeatherApiKey.");
    }

    public async ValueTask<WeatherData?> GetWeatherAsync()
    {
        using var client = _httpClientFactory.CreateClient();

        try
        {
            _logger.LogDebug("Getting weather from weatherapi.");
            var response = await client.GetAsync($"https://api.weatherapi.com/v1/current.json?key={_apiKey}&q=Batumi&aqi=no");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("Got weather from weatherapi: {WeatherContent}", content);

            var raw = JsonSerializer.Deserialize<RawWeatherData>(content)
                ?? throw new InvalidOperationException("Could not get the weather.");

            var data = new WeatherData(
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

            _logger.LogDebug("Got weather: {@Weather}", data);

            return data;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to get the weather.");
            return null;
        }
    }
}

public interface IWeatherStore
{
    ValueTask<WeatherData?> GetWeatherDataForAsync(DateTime time);
    ValueTask StoreAsync(WeatherData data);
}

public sealed class WeatherGatherer
{
    private readonly ILogger<WeatherGatherer> _logger;
    private readonly IWeatherRetriever _weatherRetriever;
    private readonly IWeatherStore _weatherStore;
    private readonly Task _gathering;
    private WeatherData? _lastData;

    public WeatherGatherer(
        ILogger<WeatherGatherer> logger,
        IWeatherRetriever weatherRetriever,
        IWeatherStore weatherStore)
    {
        _logger = logger;
        _weatherRetriever = weatherRetriever;
        _weatherStore = weatherStore;
        _gathering = GatherAsync();
    }

    private async Task GatherAsync()
    {
        _logger.LogInformation("Started gathering weather.");

        while (true)
        {
            using var _ = _logger.AddScoped("WeatherTrace", Guid.NewGuid().ToString())
                .BeginScope();

            try
            {
                await GatherWeatherAsync();
                _logger.LogDebug("Successfully gathered weather.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to gather and/or store the weather.");
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
            _logger.LogInformation("Gathered weather that is different from last recorded value. Saving it.");
            await _weatherStore.StoreAsync(data);
            _lastData = data;
            _logger.LogInformation("Successfully saved the weather.");
        }
        else
        {
            _logger.LogDebug("Weather doesn't differ from the last recorded value, skipping gathering.");
        }
    }
}
