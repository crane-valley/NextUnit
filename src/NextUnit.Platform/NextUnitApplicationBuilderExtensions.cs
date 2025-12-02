using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace NextUnit.Platform;

/// <summary>
/// Provides extension methods for configuring the NextUnit test framework with the Microsoft.Testing.Platform application builder.
/// </summary>
public static class NextUnitApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the NextUnit test framework to the test application.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    /// <returns>The test application builder for method chaining.</returns>
    public static ITestApplicationBuilder AddNextUnit(this ITestApplicationBuilder builder)
    {
        builder.RegisterTestFramework(
            _ => new NextUnitFrameworkCapabilities(),
            (caps, services) => new NextUnitFramework(caps, services));

        return builder;
    }
}

/// <summary>
/// Provides capability information for the NextUnit test framework.
/// </summary>
internal sealed class NextUnitFrameworkCapabilities : ITestFrameworkCapabilities
{
    /// <summary>
    /// Gets the collection of test framework capabilities.
    /// </summary>
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } = Array.Empty<ITestFrameworkCapability>();
}
