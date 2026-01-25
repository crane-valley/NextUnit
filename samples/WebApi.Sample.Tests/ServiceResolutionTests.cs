using NextUnit;
using NextUnit.AspNetCore;

namespace WebApi.Sample.Tests;

/// <summary>
/// Tests demonstrating service resolution from the test fixture.
/// </summary>
[NotInParallel("WebApplicationFactory")]
public class ServiceResolutionTests : WebApplicationTest<Program>
{
    [Test]
    public void GetRequiredService_ReturnsWeatherService()
    {
        var weatherService = GetRequiredService<IWeatherService>();

        Assert.NotNull(weatherService);
    }

    [Test]
    public void GetService_ReturnsWeatherService()
    {
        var weatherService = GetService<IWeatherService>();

        Assert.NotNull(weatherService);
    }

    [Test]
    public void GetService_UnregisteredService_ReturnsNull()
    {
        var service = GetService<IUnregisteredService>();

        Assert.Null(service);
    }

    [Test]
    public void CreateScope_AllowsScopedServiceResolution()
    {
        using var scope = CreateScope();
        var weatherService = scope.ServiceProvider.GetService(typeof(IWeatherService));

        Assert.NotNull(weatherService);
    }

    [Test]
    public void WeatherService_GetForecast_ReturnsData()
    {
        var weatherService = GetRequiredService<IWeatherService>();

        var forecasts = weatherService.GetForecast();

        Assert.NotEmpty(forecasts);
    }
}

// Dummy interface for testing unregistered service
public interface IUnregisteredService { }
