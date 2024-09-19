using System;
using System.Linq;
using KartMan.Api;
using KartMan.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<NpgsqlDataSource>(p =>
{
    var connectionString = builder.Configuration["DbConnectionString"];
    var sqlBuilder = new NpgsqlDataSourceBuilder(connectionString);
    sqlBuilder.ConnectionStringBuilder.IncludeErrorDetail = true;
    return sqlBuilder.Build();
});
builder.Services.AddTransient<IWeatherStore, WeatherStore>();
builder.Services.AddTransient<IWeatherRetriever, WeatherRetriever>();
builder.Services.AddSingleton<WeatherGatherer>();
builder.Services.AddSingleton<HistoryDataRepository>(); // It is a singleton because it needs locking / synchronization.
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
app.Services.GetRequiredService<WeatherGatherer>();

app.MapGet("/diag", () =>
{
    return DateTime.UtcNow;
});

var repository = app.Services.GetRequiredService<HistoryDataRepository>();

app.MapGet("/api/sessions-ng/{dateString}", async (string dateString) =>
{
    var date = dateString == "today"
        ? DateTime.UtcNow
        : DateTime.ParseExact(dateString, "dd-MM-yyyy", null);

    var infos = await repository.GetSessionInfosForDay(DateOnly.FromDateTime(date));

    // Should already be ordered but consider moving that logic here.
    return infos;
});

app.MapGet("/api/history-ng/{sessionId}", async (string sessionId) =>
{
    var history = await repository.GetHistoryForSessionAsync(sessionId);

    return history
        .GroupBy(x => x.Kart)
        .Select(g => new
        {
            First = g.First(),
            Entries = g
        })
        .Select(g => new
        {
            KartId = g.First.Kart, // TODO: Return database kart id for specific kart drive entity.
            KartName = g.First.Kart,
            Laps = g.Entries.Select(l => new
            {
                LapId = l.LapId,
                LapNumber = l.LapNumber,
                LapTime = l.LapTime,
                IsInvalidLap = l.InvalidLap
            }).OrderBy(x => x.LapNumber).ToList()
        })
        .OrderBy(x => x.KartName) // TODO: Order so 14 is after 2, not before.
        .ToList();
});

app.MapPut("/api/history-ng/{sessionId}/{lapId}/invalid", async (long lapId) =>
{
    await repository.UpdateLapInvalidStatusAsync(lapId, true);
});

app.MapPut("/api/history-ng/{sessionId}/{lapId}/valid", async (long lapId) =>
{
    await repository.UpdateLapInvalidStatusAsync(lapId, false);
});

app.MapGet("/api/total-laps", async () =>
{
    return await repository.GetTotalLapsDrivenAsync();
});

app.MapGet("/api/first-date", async () =>
{
    return await repository.GetFirstRecordedTimeAsync();
});

app.UseCors("Cors");
await app.RunAsync();

public record SessionInfoNg(
    string SessionId,
    string Name,
    DateTime StartedAt,
    WeatherInfoNg WeatherInfo);

public record WeatherInfoNg(
    decimal? AirTempC);

public record KartDriveNg(
    long LapId,
    string Kart,
    int LapNumber,
    decimal LapTime,
    bool InvalidLap);
