using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NextUnit.AspNetCore;

/// <summary>
/// Base class for ASP.NET Core integration tests using NextUnit.
/// </summary>
/// <typeparam name="TEntryPoint">The entry point class of the application under test, typically the Program class.</typeparam>
/// <remarks>
/// <para>
/// This class provides a test fixture that creates a <see cref="TestWebApplicationFactory{TEntryPoint}"/>
/// and <see cref="HttpClient"/> for testing ASP.NET Core applications. The factory and client are
/// lazily initialized on first access.
/// </para>
/// <para>
/// <strong>Important:</strong> Derived test classes should add <c>[NotInParallel("WebApplicationFactory")]</c>
/// to prevent concurrent execution issues. The NextUnit source generator does not traverse base classes
/// for attributes, so the attribute must be applied to the concrete test class.
/// </para>
/// <example>
/// <code>
/// [NotInParallel("WebApplicationFactory")]
/// public class MyApiTests : WebApplicationTest&lt;Program&gt;
/// {
///     [Test]
///     public async Task GetWeather_ReturnsOk()
///     {
///         var response = await Client.GetAsync("/weatherforecast");
///         Assert.Equal(HttpStatusCode.OK, response.StatusCode);
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class WebApplicationTest<TEntryPoint> : IDisposable, IAsyncDisposable
    where TEntryPoint : class
{
    private TestWebApplicationFactory<TEntryPoint>? _factory;
    private HttpClient? _client;
    private bool _disposed;
    private readonly object _initializationLock = new();

    /// <summary>
    /// Gets the <see cref="TestWebApplicationFactory{TEntryPoint}"/> used by this test class.
    /// The factory is lazily initialized on first access.
    /// </summary>
    protected TestWebApplicationFactory<TEntryPoint> Factory
    {
        get
        {
            if (_factory is null)
            {
                lock (_initializationLock)
                {
                    _factory ??= CreateFactory();
                }
            }
            return _factory;
        }
    }

    /// <summary>
    /// Gets the <see cref="HttpClient"/> configured for this test.
    /// A new client is created on first access. To get a fresh client, call <see cref="CreateClient"/>.
    /// </summary>
    protected HttpClient Client
    {
        get
        {
            if (_client is null)
            {
                lock (_initializationLock)
                {
                    _client ??= CreateClient();
                }
            }
            return _client;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the factory has been initialized.
    /// </summary>
    protected bool IsFactoryInitialized => _factory is not null;

    /// <summary>
    /// Gets a value indicating whether the client has been initialized.
    /// </summary>
    protected bool IsClientInitialized => _client is not null;

    /// <summary>
    /// Called to configure the web host builder before the factory is created.
    /// Override this method to customize the web host configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IWebHostBuilder"/> to configure.</param>
    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <summary>
    /// Called to configure test services for the application.
    /// Override this method to replace services with mocks or test implementations.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Called to configure the <see cref="HttpClient"/> after it is created.
    /// Override this method to set default headers, base address, or other client options.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> to configure.</param>
    protected virtual void ConfigureClient(HttpClient client)
    {
    }

    /// <summary>
    /// Creates the <see cref="TestWebApplicationFactory{TEntryPoint}"/>.
    /// Override this method to customize factory creation.
    /// </summary>
    /// <returns>A new <see cref="TestWebApplicationFactory{TEntryPoint}"/>.</returns>
    protected virtual TestWebApplicationFactory<TEntryPoint> CreateFactory()
    {
        return new TestWebApplicationFactory<TEntryPoint>()
            .WithWebHostBuilder(ConfigureWebHost)
            .WithTestServices(ConfigureTestServices);
    }

    /// <summary>
    /// Creates a new <see cref="HttpClient"/> from the factory.
    /// </summary>
    /// <returns>A new <see cref="HttpClient"/>.</returns>
    protected virtual HttpClient CreateClient()
    {
        var client = Factory.CreateClient();
        ConfigureClient(client);
        return client;
    }

    /// <summary>
    /// Gets a required service from the application's service provider.
    /// </summary>
    /// <typeparam name="TService">The type of service to retrieve.</typeparam>
    /// <returns>The requested service instance.</returns>
    protected TService GetRequiredService<TService>() where TService : notnull
    {
        return Factory.GetRequiredService<TService>();
    }

    /// <summary>
    /// Gets a service from the application's service provider, or null if not registered.
    /// </summary>
    /// <typeparam name="TService">The type of service to retrieve.</typeparam>
    /// <returns>The requested service instance, or null if not registered.</returns>
    protected TService? GetService<TService>()
    {
        return Factory.GetService<TService>();
    }

    /// <summary>
    /// Creates a new service scope for resolving scoped services.
    /// </summary>
    /// <returns>A new <see cref="IServiceScope"/>.</returns>
    protected IServiceScope CreateScope()
    {
        return Factory.CreateScope();
    }

    /// <summary>
    /// Creates an <see cref="AsyncServiceScope"/> for resolving scoped services asynchronously.
    /// </summary>
    /// <returns>A new <see cref="AsyncServiceScope"/>.</returns>
    protected AsyncServiceScope CreateAsyncScope()
    {
        return Factory.CreateAsyncScope();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Call DisposeAsync synchronously to avoid blocking issues with WebApplicationFactory
        DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await DisposeAsyncCore();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously releases the managed resources used by the test class.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
            _factory = null;
        }

        if (_client is not null)
        {
            _client.Dispose();
            _client = null;
        }
    }
}
