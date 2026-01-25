using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace NextUnit.AspNetCore;

/// <summary>
/// Enhanced <see cref="WebApplicationFactory{TEntryPoint}"/> with additional utility methods
/// for integration testing with NextUnit.
/// </summary>
/// <typeparam name="TEntryPoint">The entry point class of the application under test, typically the Program class.</typeparam>
public class TestWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private Action<IWebHostBuilder>? _webHostConfiguration;
    private Action<IServiceCollection>? _testServicesConfiguration;

    /// <summary>
    /// Configures the web host builder for this factory.
    /// </summary>
    /// <param name="configuration">The configuration action to apply to the web host builder.</param>
    /// <returns>This factory instance for method chaining.</returns>
    public new TestWebApplicationFactory<TEntryPoint> WithWebHostBuilder(Action<IWebHostBuilder> configuration)
    {
        _webHostConfiguration = configuration;
        return this;
    }

    /// <summary>
    /// Configures test services for this factory.
    /// </summary>
    /// <param name="configuration">The configuration action to apply to the service collection.</param>
    /// <returns>This factory instance for method chaining.</returns>
    public TestWebApplicationFactory<TEntryPoint> WithTestServices(Action<IServiceCollection> configuration)
    {
        _testServicesConfiguration = configuration;
        return this;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _webHostConfiguration?.Invoke(builder);

        if (_testServicesConfiguration is not null)
        {
            builder.ConfigureTestServices(_testServicesConfiguration);
        }
    }

    /// <summary>
    /// Gets a required service from the application's service provider.
    /// </summary>
    /// <typeparam name="TService">The type of service to retrieve.</typeparam>
    /// <returns>The requested service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not registered.</exception>
    public TService GetRequiredService<TService>() where TService : notnull
    {
        return Services.GetRequiredService<TService>();
    }

    /// <summary>
    /// Gets a service from the application's service provider, or null if not registered.
    /// </summary>
    /// <typeparam name="TService">The type of service to retrieve.</typeparam>
    /// <returns>The requested service instance, or null if not registered.</returns>
    public TService? GetService<TService>()
    {
        return Services.GetService<TService>();
    }

    /// <summary>
    /// Creates a new service scope for resolving scoped services.
    /// </summary>
    /// <returns>A new <see cref="IServiceScope"/>.</returns>
    public IServiceScope CreateScope()
    {
        return Services.CreateScope();
    }

    /// <summary>
    /// Creates an <see cref="AsyncServiceScope"/> for resolving scoped services asynchronously.
    /// </summary>
    /// <returns>A new <see cref="AsyncServiceScope"/>.</returns>
    public AsyncServiceScope CreateAsyncScope()
    {
        return Services.CreateAsyncScope();
    }
}
