using System.Reflection;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins the version reported by the platform extensions to the assembly informational version, so the
/// two values that used to be hardcoded ("1.2.0" and "1.6.2") cannot drift behind the package version
/// again.
/// </summary>
public sealed class PlatformVersionTests
{
    private static string ExpectedVersion()
    {
        var informational = typeof(NextUnitFramework).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        Assert.NotNull(informational);

        // Assert.NotNull takes object? and carries no flow-analysis annotation, so narrow explicitly
        // rather than suppressing: these tests are meaningless without the attribute.
        var version = informational ?? throw new InvalidOperationException(
            "AssemblyInformationalVersionAttribute is missing from NextUnit.Platform.");

        // Build metadata ("+<commit sha>") is not part of the reported version.
        var metadataStart = version.IndexOf('+');
        return metadataStart < 0 ? version : version[..metadataStart];
    }

    [Test]
    public void PlatformVersion_MatchesAssemblyInformationalVersion()
    {
        Assert.Equal(ExpectedVersion(), PlatformVersion.Value);
    }

    [Test]
    public void CommandLineOptionsProviderVersion_MatchesAssemblyInformationalVersion()
    {
        var provider = new NextUnitCommandLineOptionsProvider();

        Assert.Equal(ExpectedVersion(), provider.Version);
    }

    // Constructing the framework reads filter environment variables; see FilterEnvironmentConstraint.
    [Test]
    [NotInParallel(FilterEnvironmentConstraint.Key)]
    public void FrameworkVersion_MatchesAssemblyInformationalVersion()
    {
        var framework = new NextUnitFramework(null!, new NullServiceProvider());

        Assert.Equal(ExpectedVersion(), framework.Version);
    }

    [Test]
    public void PlatformVersion_CarriesNoBuildMetadata()
    {
        Assert.False(PlatformVersion.Value.Contains('+'));
    }
}
