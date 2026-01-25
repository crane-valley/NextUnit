var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IWeatherService, WeatherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/weatherforecast", (IWeatherService weatherService) =>
{
    return weatherService.GetForecast();
})
.WithName("GetWeatherForecast");

app.MapGet("/weatherforecast/{city}", (string city, IWeatherService weatherService) =>
{
    var forecast = weatherService.GetForecastForCity(city);
    return forecast is null
        ? Results.NotFound(new { message = $"City '{city}' not found" })
        : Results.Ok(forecast);
})
.WithName("GetWeatherForecastByCity");

app.Run();

// Services
public interface IWeatherService
{
    IEnumerable<WeatherForecast> GetForecast();
    WeatherForecast? GetForecastForCity(string city);
}

public class WeatherService : IWeatherService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private static readonly string[] Cities = ["Tokyo", "New York", "London", "Paris", "Sydney"];

    public IEnumerable<WeatherForecast> GetForecast()
    {
        return Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                Cities[index - 1],
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();
    }

    public WeatherForecast? GetForecastForCity(string city)
    {
        if (!Cities.Contains(city, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return new WeatherForecast(
            city,
            DateOnly.FromDateTime(DateTime.Now),
            Random.Shared.Next(-20, 55),
            Summaries[Random.Shared.Next(Summaries.Length)]);
    }
}

public record WeatherForecast(string City, DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Make Program partial for integration testing
public partial class Program { }
