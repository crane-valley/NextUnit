# WebApi.Sample.Tests

Sample project demonstrating ASP.NET Core integration testing with NextUnit.

## Overview

This project shows how to use `NextUnit.AspNetCore` for integration testing of ASP.NET Core Web APIs.

## Project Structure

```text
WebApi.Sample/              # The Web API under test
    Program.cs              # Minimal API endpoints, IWeatherService,
                            # WeatherService, and the WeatherForecast record

WebApi.Sample.Tests/        # Integration tests
    WebApiTestBase.cs           # Shared base class for the test classes below
    WeatherApiTests.cs          # Basic API tests
    WeatherApiWithMockTests.cs  # Service mocking examples
    ServiceResolutionTests.cs   # DI container access examples
```

## Key Concepts Demonstrated

### 1. Basic Integration Testing

`WeatherApiTests.cs` shows basic HTTP testing. It derives from the shared `WebApiTestBase`, which
derives from `WebApplicationTest<Program>` and points the host at the sample application's content
root:

```csharp
[NotInParallel("WebApplicationFactory")]
public class WeatherApiTests : WebApiTestBase
{
    [Test]
    public async Task GetWeatherForecast_ReturnsOkAsync()
    {
        var response = await Client.GetAsync("/weatherforecast");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

`[NotInParallel("WebApplicationFactory")]` sits on the concrete class, not on the base. The source
generator reads attributes from the test method and its containing type only, so an attribute on a
base class is not inherited.

### 2. Service Mocking

`WeatherApiWithMockTests.cs` shows how to replace services:

```csharp
[NotInParallel("WebApplicationFactory")]
public class WeatherApiWithMockTests : WebApiTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace<IWeatherService>(new MockWeatherService());
    }

    [Test]
    public async Task GetWeatherForecast_WithMock_ReturnsMockedDataAsync()
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
public class ServiceResolutionTests : WebApiTestBase
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
