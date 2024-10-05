using KartMan.Api.Weather;

namespace KartMan.Api.Tests.Weather;

public class WeatherGathererTests : Testing<WeatherGathererService>
{
    private readonly Mock<IWeatherRetriever> _weatherRetriever;
    private readonly Mock<IWeatherStore> _weatherStore;

    public WeatherGathererTests()
    {
        _weatherRetriever = Freeze<IWeatherRetriever>();
        _weatherStore = Freeze<IWeatherStore>();
    }

    [Theory, AutoMoqData]
    public async Task ShouldGatherWeather_OnInterval(
        WeatherData data, WeatherData newData)
    {
        _weatherRetriever.Setup(x => x.GetWeatherAsync())
            .Returns(() => new(data));

        var sut = CreateSut();

        await sut.StartAsync(Cts.Token);

        _weatherStore.Verify(x => x.StoreAsync(data));

        _weatherRetriever.Setup(x => x.GetWeatherAsync())
            .Returns(() => new(newData));
        TimeProvider.Advance(WeatherGathererService.GatherInterval);
        await WaitAsync();

        _weatherStore.Verify(x => x.StoreAsync(newData));
    }

    [Theory, AutoMoqData]
    public async Task ShouldNotGatherWeather_WhenNotChanged(
        WeatherData data)
    {
        _weatherRetriever.Setup(x => x.GetWeatherAsync())
            .Returns(() => new(data));

        var sut = CreateSut();

        await sut.StartAsync(Cts.Token);

        _weatherStore.Verify(x => x.StoreAsync(data));

        TimeProvider.Advance(WeatherGathererService.GatherInterval);
        await WaitAsync();

        _weatherStore.Verify(x => x.StoreAsync(data), Times.Once);
    }
}
