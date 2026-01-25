using System.Net;
using System.Net.Http.Json;
using NextUnit;
using NextUnit.AspNetCore;

namespace WebApi.Sample.Tests;

/// <summary>
/// Basic integration tests for the Weather API.
/// </summary>
public class WeatherApiTests : WebApplicationTest<Program>
{
    [Test]
    public async Task GetWeatherForecast_ReturnsOk()
    {
        var response = await Client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Test]
    public async Task GetWeatherForecast_ReturnsFiveForecastItems()
    {
        var forecasts = await Client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        Assert.Equal(5, forecasts!.Length);
    }

    [Test]
    public async Task GetWeatherForecastByCity_ValidCity_ReturnsOk()
    {
        var response = await Client.GetAsync("/weatherforecast/Tokyo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Test]
    public async Task GetWeatherForecastByCity_InvalidCity_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/weatherforecast/UnknownCity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Test]
    [Arguments("Tokyo")]
    [Arguments("New York")]
    [Arguments("London")]
    public async Task GetWeatherForecastByCity_ValidCities_ReturnsCorrectCity(string city)
    {
        var forecast = await Client.GetFromJsonAsync<WeatherForecast>($"/weatherforecast/{city}");

        Assert.NotNull(forecast);
        Assert.True(string.Equals(city, forecast!.City, StringComparison.OrdinalIgnoreCase));
    }
}
