using System.Net;
using KartMan.Api.Weather;
using Microsoft.Extensions.Configuration;
using Moq.Protected;

namespace KartMan.Api.Tests.Weather;

public class WeatherRetrieverTests : Testing<WeatherRetriever>
{
    [Theory, AutoMoqData]
    public async Task GetWeatherAsync_ShouldGetWeather()
    {
        var httpClientFactory = Freeze<IHttpClientFactory>();

        var handler = new Mock<HttpMessageHandler>();

        using var client = new HttpClient(handler.Object);
        httpClientFactory.Setup(x => x.CreateClient(string.Empty))
            .Returns(client);

        SetupHttpClient(handler, GetWeatherContent());

        var config = Freeze<IConfiguration>();
        config.Setup(x => x["WeatherApiKey"])
            .Returns("key");

        var sut = Fixture.Create<WeatherRetriever>();

        var weather = await sut.GetWeatherAsync();

        Assert.NotNull(weather);
        Assert.Equal(10, weather.TempC);
        Assert.Equal(1000, weather.ConditionCode);
    }

    private static void SetupHttpClient(Mock<HttpMessageHandler> handler, string shouldRespondWith)
    {
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(shouldRespondWith)
                };

                return response;
            });
    }

    private static string GetWeatherContent()
    {
        return """
            {"current": {
                "last_updated_epoch": 1673620200,
                "last_updated": "2023-01-13 06:30",
                "temp_c": 10.0,
                "temp_f": 50.0,
                "is_day": 0,
                "condition": {
                    "text": "Clear",
                    "icon": "//cdn.weatherapi.com/weather/64x64/night/113.png",
                    "code": 1000
                },
                "wind_mph": 2.2,
                "wind_kph": 3.6,
                "wind_degree": 10,
                "wind_dir": "N",
                "pressure_mb": 1020.0,
                "pressure_in": 30.13,
                "precip_mm": 0.0,
                "precip_in": 0.0,
                "humidity": 74,
                "cloud": 0,
                "feelslike_c": 10.3,
                "feelslike_f": 50.5,
                "vis_km": 16.0,
                "vis_miles": 9.0,
                "uv": 1.0,
                "gust_mph": 3.6,
                "gust_kph": 5.8
            }}
            """;
    }
}
