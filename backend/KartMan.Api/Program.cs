using System;
using KartMan.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<IWeatherRetriever, WeatherRetriever>();
builder.Services.AddTransient<IWeatherStore, WeatherStore>();
builder.Services.AddSingleton<WeatherGatherer>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/diag", () =>
{
    return DateTime.UtcNow;
});

app.Services.GetRequiredService<WeatherGatherer>();
await app.RunAsync();
