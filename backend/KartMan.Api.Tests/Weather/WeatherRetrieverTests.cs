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

        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                            "current": {
                                "temp_c": 25,
                                "condition": {
                                    "code": 1000
                                }
                            }
                        }
                    """)
                };

                return response;
            });

        var config = Freeze<IConfiguration>();
        config.Setup(x => x["WeatherApiKey"])
            .Returns("key");

        var sut = Fixture.Create<WeatherRetriever>();

        var weather = await sut.GetWeatherAsync();

        Assert.NotNull(weather);
        Assert.Equal(25, weather.TempC);
        Assert.Equal(1000, weather.ConditionCode);
    }
}
