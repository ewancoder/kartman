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

    public static bool AreEqual(WeatherData left, WeatherData right)
        => FromWeatherData(left) == FromWeatherData(right);
}

public sealed class WeatherGathererService : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<WeatherGathererService> _logger;
    private readonly IWeatherRetriever _weatherRetriever;
    private readonly IWeatherStore _weatherStore;
    private CancellationTokenSource? _combinedCts;
    private bool _isRunning = true;
    private Task? _gathering;

    /// <summary>
    /// This prevents storing the same weather data multiple times.
    /// After app restart, we still store the same weather. But it's ok.
    /// </summary>
    private WeatherData? _lastData;

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
        _combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cts.Token);

        _gathering = GatherAsync(_combinedCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        await _cts.CancelAsync();
    }

    public void Dispose()
    {
        _combinedCts?.Dispose();
        _cts.Dispose();
    }

    private async Task GatherAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Started gathering weather");

        while (_isRunning)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var _ = _logger.AddScoped("WeatherTrace", Guid.NewGuid().ToString())
                .BeginScope();

            try
            {
                await GatherWeatherAsync();
                _logger.LogDebug("Successfully gathered weather");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to gather and/or store the weather");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }
    }

    private async Task GatherWeatherAsync()
    {
        var data = await _weatherRetriever.GetWeatherAsync();
        if (data == null)
        {
            _logger.LogWarning("Could not get the Weather from weather API.");
            return;
        }

        if (_lastData is null || !WeatherComparison.AreEqual(_lastData, data))
        {
            _logger.LogInformation("Gathered weather that is different from last recorded value. Saving it");
            await _weatherStore.StoreAsync(data);
            _lastData = data;
            _logger.LogInformation("Successfully saved the weather");
        }
        else
        {
            _logger.LogTrace("Weather doesn't differ from the last recorded value, skipping gathering");
        }
    }
}
