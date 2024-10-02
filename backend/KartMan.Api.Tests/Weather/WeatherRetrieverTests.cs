using System.Net;
using KartMan.Api.Weather;
using Microsoft.Extensions.Configuration;
using Moq.Protected;

namespace KartMan.Api.Tests.Weather;

public class WeatherRetrieverTests : Testing<WeatherRetriever>
{
    [Fact] public Task ShouldHave_Cloud() => ShouldHave(x => x.Cloud == 0.4m);
    [Fact] public Task ShouldHave_ConditionCode() => ShouldHave(x => x.ConditionCode == 1000);
    [Fact] public Task ShouldHave_ConditionText() => ShouldHave(x => x.ConditionText == "Clear");
    [Fact] public Task ShouldHave_DewPointC() => ShouldHave(x => x.DewPointC == 5);
    [Fact] public Task ShouldHave_FeelsLikeC() => ShouldHave(x => x.FeelsLikeC == 10.3m);
    [Fact] public Task ShouldHave_Humidity() => ShouldHave(x => x.Humidity == 74);
    [Fact] public Task ShouldHave_UnsetId() => ShouldHave(x => x.Id == 0); // Unset.
    [Fact] public Task ShouldHave_IsDay() => ShouldHave(x => !x.IsDay);
    [Fact] public Task ShouldHave_PrecipitationMm() => ShouldHave(x => x.PrecipitationMm == 11.3m);
    [Fact] public Task ShouldHave_PressureMb() => ShouldHave(x => x.PressureMb == 1020);
    [Fact] public Task ShouldHave_TempC() => ShouldHave(x => x.TempC == 10);
    [Fact] public Task ShouldHave_WindDegree() => ShouldHave(x => x.WindDegree == 10);
    [Fact] public Task ShouldHave_WindKph() => ShouldHave(x => x.WindKph == 3.6m);
    [Fact] public Task ShouldHave_Timestamp() => ShouldHave(x => x.TimestampUtc == TimeProvider.GetUtcNow().UtcDateTime);

    [Fact] public async Task ShouldReturnNull_WhenSomethingFails()
    {
        SetupHttpClient(HttpStatusCode.InternalServerError);
        SetupWeatherApiKey("key");

        var sut = Fixture.Create<WeatherRetriever>();
        var weather = await sut.GetWeatherAsync();
        Assert.Null(weather);

        SetupHttpClient("invalid json");
        weather = await sut.GetWeatherAsync();
        Assert.Null(weather);
    }

    protected async Task ShouldHave(
        Func<WeatherData, bool> predicate)
    {
        var sut = SetupSut();
        var weather = await sut.GetWeatherAsync();

        Assert.NotNull(weather);
        Assert.True(predicate(weather));
    }

    private WeatherRetriever SetupSut()
    {
        SetupHttpClient(GetWeatherContent());
        SetupWeatherApiKey("key");

        return Fixture.Create<WeatherRetriever>();
    }

    private void SetupWeatherApiKey(string key)
    {
        var config = Freeze<IConfiguration>();
        config.Setup(x => x["WeatherApiKey"])
            .Returns(key);
    }

    private void SetupHttpClient(string shouldRespondWith)
    {
        var httpClientFactory = Freeze<IHttpClientFactory>();

        var handler = new Mock<HttpMessageHandler>();
        var client = new HttpClient(handler.Object);

        httpClientFactory.Setup(x => x.CreateClient(string.Empty))
            .Returns(client);

        SetupHttpClient(handler, shouldRespondWith);
    }

    private void SetupHttpClient(HttpStatusCode statusCode)
    {
        var httpClientFactory = Freeze<IHttpClientFactory>();

        var handler = new Mock<HttpMessageHandler>();
        var client = new HttpClient(handler.Object);

        httpClientFactory.Setup(x => x.CreateClient(string.Empty))
            .Returns(client);

        SetupHttpClient(handler, statusCode);
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

    private static void SetupHttpClient(Mock<HttpMessageHandler> handler, HttpStatusCode statusCode)
    {
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                var response = new HttpResponseMessage(statusCode);

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
                "precip_mm": 11.3,
                "precip_in": 0.0,
                "humidity": 74,
                "cloud": 0.4,
                "feelslike_c": 10.3,
                "feelslike_f": 50.5,
                "vis_km": 16.0,
                "vis_miles": 9.0,
                "uv": 1.0,
                "gust_mph": 3.6,
                "gust_kph": 5.8,
                "dewpoint_c": 5
            }}
            """;
    }
}
