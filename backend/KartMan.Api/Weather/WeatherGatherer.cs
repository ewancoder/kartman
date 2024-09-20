namespace KartMan.Api.Weather;

/// <summary>
/// Used to check whether weather has changed before storing a new value.
/// </summary>
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
    decimal DewPointC)
{
    public static WeatherComparison FromWeatherData(WeatherData weatherData)
    {
        return new(
            weatherData.TempC,
            weatherData.IsDay,
            weatherData.ConditionCode,
            weatherData.WindKph,
            weatherData.WindDegree,
            weatherData.PressureMb,
            weatherData.PrecipitationMm,
            weatherData.Humidity,
            weatherData.Cloud,
            weatherData.FeelsLikeC,
            weatherData.DewPointC);
    }
}



public sealed class WeatherGathererService : IHostedService
{
    private readonly ILogger<WeatherGathererService> _logger;
    private readonly IWeatherRetriever _weatherRetriever;
    private readonly IWeatherStore _weatherStore;
    private WeatherData? _lastData;
    private Task? _gathering;
    private bool _isRunning = true;

    public WeatherGathererService(
        ILogger<WeatherGathererService> logger,
        IWeatherRetriever weatherRetriever,
        IWeatherStore weatherStore)
    {
        _logger = logger;
        _weatherRetriever = weatherRetriever;
        _weatherStore = weatherStore;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _gathering = GatherAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // TODO: Cancel local CTS here to stop Task.Delay waiting for one minute.
        _isRunning = false;
    }

    private async Task GatherAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started gathering weather.");

        while (_isRunning)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        if (_lastData == null || WeatherComparison.FromWeatherData(_lastData) != WeatherComparison.FromWeatherData(data))
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
