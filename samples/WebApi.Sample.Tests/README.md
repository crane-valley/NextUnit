# WebApi.Sample.Tests

Sample project demonstrating ASP.NET Core integration testing with NextUnit.

## Overview

This project shows how to use `NextUnit.AspNetCore` for integration testing of ASP.NET Core Web APIs.

## Project Structure

```text
WebApi.Sample/              # The Web API under test
    Program.cs              # Minimal API with weather endpoints
    IWeatherService.cs      # Service interface
    WeatherService.cs       # Service implementation

WebApi.Sample.Tests/        # Integration tests
    WeatherApiTests.cs          # Basic API tests
    WeatherApiWithMockTests.cs  # Service mocking examples
    ServiceResolutionTests.cs   # DI container access examples
```

## Key Concepts Demonstrated

### 1. Basic Integration Testing

`WeatherApiTests.cs` shows basic HTTP testing:

```csharp
[NotInParallel("WebApplicationFactory")]
public class WeatherApiTests : WebApplicationTest<Program>
{
    [Test]
    public async Task GetWeatherForecast_ReturnsOk()
    {
        var response = await Client.GetAsync("/weatherforecast");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### 2. Service Mocking

`WeatherApiWithMockTests.cs` shows how to replace services:

```csharp
[NotInParallel("WebApplicationFactory")]
public class WeatherApiWithMockTests : WebApplicationTest<Program>
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace<IWeatherService>(new MockWeatherService());
    }

    [Test]
    public async Task GetWeatherForecast_WithMock_ReturnsMockedData()
    {
        var forecasts = await Client.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast");
        Assert.Equal("MockCity", forecasts![0].City);
    }
}
```

### 3. Service Resolution

`ServiceResolutionTests.cs` shows how to access services:

```csharp
[NotInParallel("WebApplicationFactory")]
public class ServiceResolutionTests : WebApplicationTest<Program>
{
    [Test]
    public void GetRequiredService_ReturnsWeatherService()
    {
        var weatherService = GetRequiredService<IWeatherService>();
        Assert.NotNull(weatherService);
    }
}
```

## Running the Tests

```bash
# From the repository root
dotnet test samples/WebApi.Sample.Tests

# Or using Visual Studio Test Explorer
```

## Dependencies

- `NextUnit` - Test framework
- `NextUnit.AspNetCore` - ASP.NET Core integration

## See Also

- [ASP.NET Core Testing Guide](../../docs/ASPNETCORE_TESTING.md)
- [Getting Started](../../docs/GETTING_STARTED.md)
