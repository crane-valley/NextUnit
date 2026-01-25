# ASP.NET Core Integration Testing with NextUnit

This guide covers how to write integration tests for ASP.NET Core applications using NextUnit.

## Installation

Install the `NextUnit.AspNetCore` package:

```bash
dotnet add package NextUnit.AspNetCore
```

This package provides:

- `WebApplicationTest<TEntryPoint>` - Base class for integration tests
- `TestWebApplicationFactory<TEntryPoint>` - Enhanced WebApplicationFactory
- `ServiceCollectionExtensions` - Helpers for service mocking

## Quick Start

### Basic Integration Test

```csharp
using System.Net;
using NextUnit;
using NextUnit.AspNetCore;

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

### Key Points

1. **Inherit from `WebApplicationTest<TEntryPoint>`** - `TEntryPoint` is typically your `Program` class
2. **Add `[NotInParallel("WebApplicationFactory")]`** - Required on each test class (not inherited from base)
3. **Use `Client` property** - Pre-configured `HttpClient` for making requests

## WebApplicationTest Base Class

The `WebApplicationTest<TEntryPoint>` base class provides:

### Properties

| Property | Description |
|----------|-------------|
| `Factory` | The `TestWebApplicationFactory<TEntryPoint>` instance (lazily initialized) |
| `Client` | Pre-configured `HttpClient` (lazily initialized) |
| `IsFactoryInitialized` | Whether the factory has been created |
| `IsClientInitialized` | Whether the client has been created |

### Virtual Methods for Customization

Override these methods to customize behavior:

```csharp
[NotInParallel("WebApplicationFactory")]
public class CustomizedTests : WebApplicationTest<Program>
{
    // Configure the web host (e.g., change environment, logging)
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    // Replace services with mocks
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.Replace<IWeatherService>(new MockWeatherService());
    }

    // Configure the HttpClient (e.g., set headers)
    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Test-Header", "value");
    }
}
```

### Service Resolution

Access services from the application's DI container:

```csharp
[Test]
public void CanResolveServices()
{
    // Get required service (throws if not registered)
    var weatherService = GetRequiredService<IWeatherService>();

    // Get optional service (returns null if not registered)
    var optionalService = GetService<IOptionalService>();

    // Create a scope for scoped services
    using var scope = CreateScope();
    var scopedService = scope.ServiceProvider.GetRequiredService<IScopedService>();
}
```

## Service Mocking

### Using ServiceCollectionExtensions

The `ServiceCollectionExtensions` class provides convenient methods for replacing services:

```csharp
protected override void ConfigureTestServices(IServiceCollection services)
{
    // Replace with an instance
    services.Replace<IWeatherService>(new MockWeatherService());

    // Replace with implementation type
    services.Replace<IWeatherService, MockWeatherService>(ServiceLifetime.Scoped);

    // Replace with factory
    services.Replace<IWeatherService>(sp => new MockWeatherService(), ServiceLifetime.Singleton);

    // Remove all registrations
    services.RemoveAll<IWeatherService>();
}
```

### Complete Mock Example

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

        Assert.NotNull(forecasts);
        Assert.Single(forecasts!);
        Assert.Equal("MockCity", forecasts![0].City);
    }
}

public class MockWeatherService : IWeatherService
{
    public IEnumerable<WeatherForecast> GetForecast()
    {
        return [new WeatherForecast("MockCity", DateOnly.FromDateTime(DateTime.Now), 25, "Sunny")];
    }
}
```

## TestWebApplicationFactory

For more control, use `TestWebApplicationFactory<TEntryPoint>` directly:

```csharp
[NotInParallel("WebApplicationFactory")]
public class AdvancedTests
{
    [Test]
    public async Task DirectFactoryUsage()
    {
        await using var factory = new TestWebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            })
            .WithTestServices(services =>
            {
                services.Replace<IWeatherService>(new MockWeatherService());
            });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### Fluent API

| Method | Description |
|--------|-------------|
| `WithWebHostBuilder(Action<IWebHostBuilder>)` | Configure the web host |
| `WithTestServices(Action<IServiceCollection>)` | Configure test services |
| `GetRequiredService<T>()` | Get a required service |
| `GetService<T>()` | Get an optional service |
| `CreateScope()` | Create a service scope |
| `CreateAsyncScope()` | Create an async service scope |

## Important Notes

### NotInParallel Attribute

**The `[NotInParallel("WebApplicationFactory")]` attribute must be applied to each concrete test class.**

NextUnit's source generator does not traverse base classes for attributes, so the attribute on `WebApplicationTest<TEntryPoint>` is not inherited. Always add it to your test classes:

```csharp
// Required - attribute must be on the concrete class
[NotInParallel("WebApplicationFactory")]
public class MyApiTests : WebApplicationTest<Program>
{
    // ...
}
```

### Lazy Initialization

The `Factory` and `Client` properties are lazily initialized on first access. This means:

- No resources are allocated until you use them
- You can safely override configuration methods before accessing properties
- Disposal is handled automatically via `IDisposable`/`IAsyncDisposable`

### Disposal

`WebApplicationTest<TEntryPoint>` implements both `IDisposable` and `IAsyncDisposable`. NextUnit handles disposal automatically after each test.

## Best Practices

1. **Use `[NotInParallel]` consistently** - All tests sharing the same `WebApplicationFactory` constraint key run serially
2. **Mock external dependencies** - Replace services that make network calls or access databases
3. **Use scopes for scoped services** - Create a scope when resolving scoped services
4. **Keep tests isolated** - Each test class gets its own factory instance

## Sample Project

See `samples/WebApi.Sample.Tests/` for complete examples:

- `WeatherApiTests.cs` - Basic integration tests
- `WeatherApiWithMockTests.cs` - Service mocking examples
- `ServiceResolutionTests.cs` - Service resolution patterns

## See Also

- [Getting Started](GETTING_STARTED.md)
- [Best Practices](BEST_PRACTICES.md)
- [Microsoft.AspNetCore.Mvc.Testing Documentation](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
