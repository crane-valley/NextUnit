using Microsoft.Testing.Platform.Builder;

namespace NextUnit.Platform;

/// <summary>
/// Hook for Microsoft.Testing.Platform to automatically configure NextUnit when building test applications.
/// This class is discovered and invoked by Microsoft.Testing.Platform.MSBuild during test host initialization.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Called by Microsoft.Testing.Platform to add NextUnit extensions to the test application builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder to configure.</param>
    /// <param name="args">Command-line arguments passed to the test application.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] args)
    {
        testApplicationBuilder.AddNextUnit();
    }
}
