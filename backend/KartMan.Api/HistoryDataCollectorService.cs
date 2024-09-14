using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KartMan.Api;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KartMan.Host;

public sealed record ShortEntry(
    int lap, decimal time);

public sealed record LapEntry(
    DateTime recordedAtUtc,
    int session,
    string totalLength,
    string kart,
    int lap,
    decimal time,
    int position,
    string? gap)
{
    public ComparisonEntry ToComparisonEntry() => new(DateOnly.FromDateTime(recordedAtUtc), session, kart, lap);

    public string GetSessionIdentifier()
    {
        return $"{DateOnly.FromDateTime(recordedAtUtc).DayNumber}-{session}";
    }

    public string SessionId => GetSessionIdentifier();
}

public sealed record RawJson(
    RawHeadInfo headinfo,
    object[][] results);

public sealed record RawHeadInfo(
    string number,
    string len);

public sealed record ComparisonEntry(DateOnly day, int session, string kart, int lap);
public sealed class HistoryDataCollectorService : IHostedService
{
    private bool _isRunning = true;
    private Task? _gatheringData;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HistoryDataRepository _repository;
    private string? _previousHash;
    private const int StartTimeHourUtc = 5; // 5 GMT, 9 AM.
    private const int EndTimeHourUtc = 19; // 19 PMT, 11 PM.
    private DateTime _lastTelemetryRecordedAtUtc;
    private string? _lastSession;
    private bool _dayEnded = false;
    private readonly ILogger<HistoryDataCollectorService> _logger;

    public HistoryDataCollectorService(
        IHttpClientFactory httpClientFactory,
        HistoryDataRepository repository,
        ILogger<HistoryDataCollectorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gatheringData = Task.Run(async () =>
        {
            while (true)
            {
                using var _ = _logger.AddScoped("KartingTrace", Guid.NewGuid().ToString())
                    .BeginScope();

                if ((DateTime.UtcNow.Hour < StartTimeHourUtc || DateTime.UtcNow.Hour >= EndTimeHourUtc)
                    && DateTime.UtcNow - _lastTelemetryRecordedAtUtc > TimeSpan.FromHours(1.5))
                {
                    _logger.LogTrace("Skipping logging of karting data because it's not a working time of day. Waiting for 5 minutes before next check.");
                    _dayEnded = true;
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    continue;
                }

                if (!_isRunning)
                    return;

                await GatherDataAsync();

                await Task.Delay(3000);
            }
        });

        _logger.LogInformation("Started gathering history data.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopped gathering history data.");
        _isRunning = false;
        return Task.CompletedTask;
    }

    private async Task GatherDataAsync()
    {
        try
        {
            _logger.LogDebug("Logging karting data.");
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://kart-timer.com/drivers/ajax.php?p=livescreen&track=110&target=updaterace");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var hash = Encoding.UTF8.GetString(MD5.HashData(Encoding.UTF8.GetBytes(content)));
            if (_previousHash == hash)
            {
                _logger.LogTrace("Karting data didn't change since last time, not logging it.");
                return;
            }

            var freshStart = _previousHash is null && _lastTelemetryRecordedAtUtc == default;

            var rawJson = JsonSerializer.Deserialize<RawJson>(content)
                ?? throw new InvalidOperationException("Could not deserialize result from karting api.");
            // TODO: Send signalr update here to update the web page, we got new data.

            // A HACK to avoid recording last day data again, if we restarted the app.
            // Will only work if there were at least 10 sessions in the last day.
            try
            {
                if (Convert.ToInt32(rawJson.headinfo.number) >= 10 && freshStart)
                    return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to check headinfo number on the fresh start.");
            }

            _previousHash = hash;
            _lastTelemetryRecordedAtUtc = DateTime.UtcNow;

            if (_dayEnded && _lastSession == rawJson.headinfo.number)
            {
                _logger.LogDebug("Day has ended and it is the last session. Skipping.");
                return; // TODO: If there were ZERO sessions for the whole day - this will cause first session of the next day to be lost. Try to fix this.
            }

            if (_dayEnded) _dayEnded = false;

            _lastSession = rawJson.headinfo.number;

            static decimal ParseTime(string time)
            {
                if (!time.Contains(":"))
                    return Convert.ToDecimal(time);

                var minutes = Convert.ToInt32(time.Split(":")[0]);
                return Convert.ToDecimal(time.Split(":")[1]) + (minutes * 60m);
            }

            var entries = rawJson.results.Select(x =>
            {
                var time = x[6]?.ToString();
                if (string.IsNullOrEmpty(time)) return null;
                var decimalTime = ParseTime(time);

                try
                {
                    // TODO: Also save NAME of person - for other kartings it works.
                    return new LapEntry(
                        DateTime.UtcNow,
                        Convert.ToInt32(rawJson.headinfo.number),
                        rawJson.headinfo.len,
                        x[2].ToString()!,
                        Convert.ToInt32(x[3].ToString()),
                        decimalTime,
                        Convert.ToInt32(x[0]?.ToString()), x[7]?.ToString()); // TODO: Figure out last 2 values. Debug.
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Error when trying to parse data for lap entry.");
                    return null;
                }
            }).Where(x => x != null).ToList();

            _logger.LogInformation("Saving karting entries to the database.");
            foreach (var entry in entries)
            {
                await _repository.SaveLapAsync(DateOnly.FromDateTime(DateTime.UtcNow), entry!);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to gather karting data on the interval.");
        }
    }
}

public enum Weather
{
    Dry = 1,
    Damp,
    Wet,
    ExtraWet
}

public enum Sky
{
    Clear = 1,
    Cloudy,
    Overcast
}

public enum Wind
{
    NoWind = 1,
    Yes = 2
}

public enum TrackTemp
{
    Cold = 1,
    Cool,
    Warm,
    Hot
}

public enum TrackConfig
{
    Short = 1,
    Long,
    ShortReverse,
    LongReverse
}

public record SessionInfo(
    Weather? Weather,
    Sky? Sky,
    Wind? Wind,
    decimal? AirTempC,
    decimal? TrackTempC,
    TrackTemp? TrackTempApproximation,
    TrackConfig? TrackConfig)
{
    public bool IsValid => Weather != null || Sky != null || Wind != null || AirTempC != null || TrackTempC != null
        || TrackTempApproximation != null || TrackConfig != null;
}
