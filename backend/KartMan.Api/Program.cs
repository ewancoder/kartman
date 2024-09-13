using System;
using System.Collections.Generic;
using System.Linq;
using KartMan.Api;
using KartMan.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<IWeatherRetriever, WeatherRetriever>();
builder.Services.AddTransient<IWeatherStore, WeatherStore>();
builder.Services.AddSingleton<WeatherGatherer>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<HistoryDataRepository>();
builder.Services.AddHostedService<HistoryDataCollectorService>();

var isDebug = false;
#if DEBUG
isDebug = true;
#endif

builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Information()
        .MinimumLevel.Override("KartMan", LogEventLevel.Verbose)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}" + (isDebug ? "\n{Properties}\n" : string.Empty) + "{NewLine}{Exception}")
        .WriteTo.File(new CompactJsonFormatter(), "logs/log.json", rollingInterval: RollingInterval.Day)
        .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
        .WriteTo.Seq("http://31.146.143.167:5341", apiKey: builder.Configuration["SeqApiKey"])
        .Enrich.WithThreadId();
});

builder.Services.AddCors(x =>
{
    x.AddPolicy("Cors", x => x
        .WithOrigins("http://localhost:4200", "https://kartman.typingrealm.com", "https://dev.kartman.typingrealm.com")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});

var app = builder.Build();
var weatherStore = app.Services.GetRequiredService<IWeatherStore>();

app.MapGet("/diag", () =>
{
    return DateTime.UtcNow;
});

app.MapGet("/api/weather/{date}", async (DateTime date) =>
{
    return await weatherStore.GetWeatherDataForAsync(date.Date);
});

var repository = app.Services.GetRequiredService<HistoryDataRepository>();

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

app.MapGet("/api/sessions-ng/{dateString}", async (string dateString) =>
{
    var date = dateString == "today"
        ? DateTime.UtcNow
        : DateTime.ParseExact(dateString, "dd-MM-yyyy", null);

    var history = await repository.GetHistoryForDayAsync(DateOnly.FromDateTime(date));

    var sessions = history
        .GroupBy(x => x.SessionId)
        .Select(x => x.OrderBy(s => s.recordedAtUtc).First())
        .Select(x => new { x.SessionId, SessionStartTime = x.recordedAtUtc })
        .ToList();

    var infos = new List<SessionInfoNg>();
    foreach (var session in sessions)
    {
        var sessionInfo = await repository.GetSessionInfoAsync(session.SessionId);

        infos.Add(new SessionInfoNg(
            session.SessionId,
            $"Session {session.SessionId.Split('-')[1]}",
            session.SessionStartTime,
            new WeatherInfoNg(
                sessionInfo?.AirTempC)));
    }

    return infos.OrderByDescending(s => s.StartedAt);
});

app.MapGet("/api/history-ng/{sessionId}", async (string sessionId) =>
{
    var history = await repository.GetHistoryForSessionAsync(sessionId);

    return history
        .GroupBy(x => x.kart)
        .Select(g => new
        {
            Kart = g.First().kart,
            KartName = g.First().kart,
            Laps = g.Select(l => new
            {
                LapNumber = l.lap,
                LapTime = l.time
            }).OrderBy(x => x.LapNumber).ToList()
        })
        .ToList();
});

app.UseCors("Cors");

app.Services.GetRequiredService<WeatherGatherer>();
await app.RunAsync();

public record SessionInfoNg(
    string SessionId,
    string Name,
    DateTime StartedAt,
    WeatherInfoNg WeatherInfo);

public record WeatherInfoNg(
    decimal? AirTempC);
