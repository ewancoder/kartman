using System.Security.Cryptography;
using System.Text;

namespace KartMan.Api;

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

sealed file record RawJson(
    RawHeadInfo headinfo,
    object[][] results);

sealed file record RawHeadInfo(
    string number,
    string len);

public sealed record ComparisonEntry(DateOnly day, int session, string kart, int lap);
public sealed class HistoryDataCollectorService : IHostedService
{
    public const int MaxRecordedLapTimeSeconds = 10 * 60;
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
    private readonly HashSet<ComparisonEntry> _cache = [];

    public HistoryDataCollectorService(
        IHttpClientFactory httpClientFactory,
        HistoryDataRepository repository,
        ILogger<HistoryDataCollectorService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (File.Exists("cache"))
        {
            var text = await File.ReadAllTextAsync("cache", cancellationToken);
            var parts = text.Split("__");
            if (parts.Length == 4)
            {
                _previousHash = parts[0];
                _lastTelemetryRecordedAtUtc = new DateTime(Convert.ToInt64(parts[1]));
                _dayEnded = Convert.ToBoolean(parts[2]);
                _lastSession = parts[3];

                _logger.LogInformation("Starting gathering process. Got data from cache: {PreviousHash}, {LastTelemetryRecordedAtUtc}, {DayEnded}, {LastSession}", _previousHash, _lastTelemetryRecordedAtUtc, _dayEnded, _lastSession);
            }
        }

        _gatheringData = Task.Run(async () =>
        {
            while (true)
            {
                using var _ = _logger.AddScoped("KartingTrace", Guid.NewGuid().ToString())
                    .BeginScope();

                if ((DateTime.UtcNow.Hour < StartTimeHourUtc || DateTime.UtcNow.Hour >= EndTimeHourUtc)
                    && DateTime.UtcNow - _lastTelemetryRecordedAtUtc > TimeSpan.FromHours(1.5))
                {
                    _logger.LogTrace("Skipping logging of karting data because it's not a working time of day. Waiting for 5 minutes before next check");
                    _dayEnded = true;
                    await Task.Delay(TimeSpan.FromMinutes(5));

                    _cache.Clear(); // Clear the cache daily to not accumulate a lot of entries in memory.
                    continue;
                }

                if (!_isRunning)
                    return;

                // The time is between 8am and 10pm.
                await GatherDataAsync();

                await Task.Delay(3000);
            }
        }, cancellationToken);

        _logger.LogInformation("Started gathering history data");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopped gathering history data");
        _isRunning = false;
        return Task.CompletedTask;
    }

    private async Task GatherDataAsync()
    {
        try
        {
            _logger.LogDebug("Logging karting data");
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://kart-timer.com/drivers/ajax.php?p=livescreen&track=110&target=updaterace");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var hash = Encoding.UTF8.GetString(MD5.HashData(Encoding.UTF8.GetBytes(content)));
            if (_previousHash == hash)
            {
                _logger.LogTrace("Karting data didn't change since last time, not logging it");
                return;
            }

            var rawJson = JsonSerializer.Deserialize<RawJson>(content)
                ?? throw new InvalidOperationException("Could not deserialize result from karting api.");
            // TODO: Send signalr update here to update the web page, we got new data.

            try
            {
                _previousHash = hash;
                _lastTelemetryRecordedAtUtc = DateTime.UtcNow;

                if (_dayEnded && _lastSession == rawJson.headinfo.number && _lastSession != "1")
                {
                    _logger.LogDebug("Day has ended and it is the last session. Skipping");
                    return; // TODO: If there was only one session for the whole day - we'll write invalid data after all.
                }

                _logger.LogInformation("Storing the data: {DayEndend}, {LastSession}, {CurrentSessionNumber}", _dayEnded, _lastSession, rawJson.headinfo.number);

                if (_dayEnded) _dayEnded = false;
                _lastSession = rawJson.headinfo.number;
            }
            finally
            {
                await File.WriteAllTextAsync("cache", $"{_previousHash}__{_lastTelemetryRecordedAtUtc.Ticks}__{_dayEnded}__{_lastSession}");
            }

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
                    _logger.LogWarning(exception, "Error when trying to parse data for lap entry");
                    return null;
                }
            }).Where(x => x != null && x.time < MaxRecordedLapTimeSeconds).ToList();

            _logger.LogInformation("Saving karting entries to the database");
            foreach (var entry in entries)
            {
                // Just a performance optimization for when the app is already running.
                // To not execute extra SQL queries every 3 seconds.
                if (_cache.Contains(entry!.ToComparisonEntry()))
                    continue;

                await _repository.SaveLapAsync(DateOnly.FromDateTime(DateTime.UtcNow), entry);

                _cache.Add(entry.ToComparisonEntry());
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to gather karting data on the interval");
        }
    }
}

