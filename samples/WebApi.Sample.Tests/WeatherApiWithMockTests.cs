using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NextUnit;
using NextUnit.AspNetCore;

namespace WebApi.Sample.Tests;

/// <summary>
/// Integration tests demonstrating service mocking with NextUnit.AspNetCore.
/// </summary>
public class WeatherApiWithMockTests : WebApplicationTest<Program>
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Replace IWeatherService with a mock implementation
        services.Replace<IWeatherService>(new MockWeatherService());
    }

    [Test]
    public async Task GetWeatherForecast_WithMock_ReturnsMockedData()
    {
        var forecasts = await Client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        var result = forecasts!;
        Assert.Single(result);
        Assert.Equal("MockCity", result[0].City);
        Assert.Equal(25, result[0].TemperatureC);
    }

    [Test]
    public async Task GetWeatherForecastByCity_WithMock_ReturnsOkForMockCity()
    {
        var response = await Client.GetAsync("/weatherforecast/MockCity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Test]
    public async Task GetWeatherForecastByCity_WithMock_ReturnsNotFoundForOtherCities()
    {
        var response = await Client.GetAsync("/weatherforecast/Tokyo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>
/// A mock implementation of IWeatherService for testing.
/// </summary>
public class MockWeatherService : IWeatherService
{
    public IEnumerable<WeatherForecast> GetForecast()
    {
        return
        [
            new WeatherForecast("MockCity", DateOnly.FromDateTime(DateTime.Now), 25, "Sunny")
        ];
    }

    public WeatherForecast? GetForecastForCity(string city)
    {
        if (city.Equals("MockCity", StringComparison.OrdinalIgnoreCase))
        {
            return new WeatherForecast("MockCity", DateOnly.FromDateTime(DateTime.Now), 25, "Sunny");
        }
        return null;
    }
}
