namespace KartMan.Api.Weather;

sealed file record RawWeatherData(
    RawCurrent current);
sealed file record RawCurrent(
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
sealed file record RawCondition(
    int code,
    string text);

public interface IWeatherRetriever
{
    /// <summary>
    /// Returns null if upstream service call was unsuccessful.
    /// </summary>
    ValueTask<WeatherData?> GetWeatherAsync();
}

public sealed class WeatherRetriever : IWeatherRetriever
{
    private readonly ILogger<WeatherRetriever> _logger;
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;

    public WeatherRetriever(
        ILogger<WeatherRetriever> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["WeatherApiKey"]
            ?? throw new InvalidOperationException("Could not get WeatherApiKey.");
        _timeProvider = timeProvider;
    }

    public async ValueTask<WeatherData?> GetWeatherAsync()
    {
        using var client = _httpClientFactory.CreateClient();

        try
        {
            _logger.LogDebug("Getting weather from WeatherAPI");
            var response = await client.GetAsync($"https://api.weatherapi.com/v1/current.json?key={_apiKey}&q=Batumi&aqi=no");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("Got weather from WeatherAPI: {@WeatherContent}", content);

            var raw = JsonSerializer.Deserialize<RawWeatherData>(content)
                ?? throw new InvalidOperationException("Could not deserialize the weather.");

            var data = new WeatherData(
                _timeProvider.GetUtcNow().UtcDateTime,
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
            _logger.LogError(exception, "Failed to retrieve the weather from WeatherAPI");
            return null;
        }
    }
}
