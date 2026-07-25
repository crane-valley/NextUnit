using System.Net;
using System.Net.Http.Json;
using NextUnit;

namespace WebApi.Sample.Tests;

/// <summary>
/// Basic integration tests for the Weather API.
/// </summary>
[NotInParallel("WebApplicationFactory")]
public class WeatherApiTests : WebApiTestBase
{
    [Test]
    public async Task GetWeatherForecast_ReturnsOkAsync()
    {
        var response = await Client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Test]
    public async Task GetWeatherForecast_ReturnsFiveForecastItemsAsync()
    {
        var forecasts = await Client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");

        Assert.NotNull(forecasts);
        Assert.Equal(5, forecasts!.Length);
    }

    [Test]
    public async Task GetWeatherForecastByCity_ValidCity_ReturnsOkAsync()
    {
        var response = await Client.GetAsync("/weatherforecast/Tokyo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Test]
    public async Task GetWeatherForecastByCity_InvalidCity_ReturnsNotFoundAsync()
    {
        var response = await Client.GetAsync("/weatherforecast/UnknownCity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Test]
    [Arguments("Tokyo")]
    [Arguments("New York")]
    [Arguments("London")]
    public async Task GetWeatherForecastByCity_ValidCities_ReturnsCorrectCityAsync(string city)
    {
        var forecast = await Client.GetFromJsonAsync<WeatherForecast>($"/weatherforecast/{city}");

        Assert.NotNull(forecast);
        Assert.True(string.Equals(city, forecast!.City, StringComparison.OrdinalIgnoreCase));
    }
}
