using System;
using System.Linq;
using KartMan.Api;
using KartMan.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<IWeatherRetriever, WeatherRetriever>();
builder.Services.AddTransient<IWeatherStore, WeatherStore>();
builder.Services.AddSingleton<WeatherGatherer>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<HistoryDataRepository>();
builder.Services.AddHostedService<HistoryDataCollectorService>();

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

app.UseCors("Cors");

app.Services.GetRequiredService<WeatherGatherer>();
await app.RunAsync();
