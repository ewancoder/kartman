using System;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/diag", () =>
{
    return DateTime.UtcNow;
});

await app.RunAsync();
