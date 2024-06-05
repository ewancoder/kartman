using System;
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

app.Services.GetRequiredService<WeatherGatherer>();
await app.RunAsync();
