using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NextUnit.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to simplify test service configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Removes all registrations of a service type from the service collection.
    /// </summary>
    /// <typeparam name="TService">The type of service to remove.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection RemoveAll<TService>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll(typeof(TService));
        return services;
    }

    /// <summary>
    /// Replaces all registrations of a service type with a new implementation type.
    /// </summary>
    /// <typeparam name="TService">The type of service to replace.</typeparam>
    /// <typeparam name="TImplementation">The new implementation type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection Replace<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<TService>();
        services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
        return services;
    }

    /// <summary>
    /// Replaces all registrations of a service type with a singleton instance.
    /// </summary>
    /// <typeparam name="TService">The type of service to replace.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <param name="instance">The singleton instance to use.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection Replace<TService>(
        this IServiceCollection services,
        TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(instance);

        services.RemoveAll<TService>();
        services.AddSingleton(instance);
        return services;
    }

    /// <summary>
    /// Replaces all registrations of a service type with a factory function.
    /// </summary>
    /// <typeparam name="TService">The type of service to replace.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <param name="implementationFactory">The factory function to create the service.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection Replace<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        services.RemoveAll<TService>();
        services.Add(new ServiceDescriptor(typeof(TService), implementationFactory, lifetime));
        return services;
    }

    /// <summary>
    /// Adds or replaces a service registration with a new implementation type.
    /// If the service is not registered, it will be added. If it is registered, all existing
    /// registrations will be replaced.
    /// </summary>
    /// <typeparam name="TService">The type of service to add or replace.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <param name="lifetime">The service lifetime. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOrReplace<TService, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
        where TImplementation : class, TService
    {
        return services.Replace<TService, TImplementation>(lifetime);
    }

    /// <summary>
    /// Adds or replaces a service registration with a singleton instance.
    /// </summary>
    /// <typeparam name="TService">The type of service to add or replace.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to modify.</param>
    /// <param name="instance">The singleton instance to use.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOrReplace<TService>(
        this IServiceCollection services,
        TService instance)
        where TService : class
    {
        return services.Replace(instance);
    }
}
