using KartMan.Host;
using Microsoft.AspNetCore.Cors.Infrastructure;

var builder = WebApplication.CreateBuilder();
builder.Services.AddSingleton<HistoryDataCollectorService>();
builder.Services.AddSingleton<HistoryDataRepository>();
builder.Services.AddSingleton<WeatherGatherer>();
builder.Services.AddSingleton<IWeatherStore, WeatherStore>();
builder.Services.AddHostedService<HistoryDataCollectorService>(x => x.GetRequiredService<HistoryDataCollectorService>());

builder.Services.AddCors(x =>
{
    x.AddPolicy("Cors", x => x
        .WithOrigins("http://localhost:4200", "https://kartman.typingrealm.com")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

var app = builder.Build();

app.Services.GetRequiredService<WeatherGatherer>();

var repository = app.Services.GetRequiredService<HistoryDataRepository>();
await repository.UpdateDatabaseAsync();
var s = app.Services.GetRequiredService<HistoryDataCollectorService>();

app.MapGet("/api/jsons", () =>
{
    return s._jsons;
});

app.MapPut("/api/sessions/{session}", async (string session, SessionInfo info) =>
{
    await repository.UpdateSessionInfoAsync(session, info);
});

app.MapGet("/api/sessions/{session}", async (string session) =>
{
    return await repository.GetSessionInfoAsync(session);
});



app.MapGet("/api/history/today", async () =>
{
    var history = await repository.GetHistoryForDayAsync(DateOnly.FromDateTime(DateTime.UtcNow));

    return history
        .GroupBy(x => x.session)
        .OrderByDescending(g => g.Max(x => x.recordedAtUtc))
        .Select(x => x
            .OrderBy(y => y.kart)
            .ThenBy(y => y.lap))
        .SelectMany(x => x)
        .ToList();
});

app.MapGet("/api/history/{session}/{kart}", async (int session, string kart) =>
{
    var history = (await repository.GetHistoryForDayAsync(DateOnly.FromDateTime(DateTime.UtcNow)))
        .Where(x => x.session == session && x.kart == kart)
        .Select(x => new ShortEntry(x.lap, x.time))
        .ToList();

    return history
        .OrderBy(x => x.lap)
        .ToList();
});

app.MapGet("/api/history/top10", async () =>
{
    return await repository.GetTopTimesAsync(10);
});

app.MapGet("/api/history/{dateString}", async (string dateString) =>
{
    var date = DateTime.ParseExact(dateString, "dd-MM-yyyy", null);
    var history = await repository.GetHistoryForDayAsync(DateOnly.FromDateTime(date));

    return history
        .GroupBy(x => x.session)
        .OrderByDescending(g => g.Max(x => x.recordedAtUtc))
        .Select(x => x
            .OrderBy(y => y.kart)
            .ThenBy(y => y.lap))
        .SelectMany(x => x)
        .ToList();
});

app.UseCors("Cors");

await app.RunAsync();

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
