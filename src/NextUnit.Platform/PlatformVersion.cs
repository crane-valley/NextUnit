using System.Reflection;

namespace NextUnit.Platform;

/// <summary>
/// Supplies the version reported by the NextUnit Microsoft.Testing.Platform extensions.
/// </summary>
/// <remarks>
/// The value is read from the assembly's informational version so every extension reports the package
/// version from <c>Directory.Build.props</c>. Hardcoded strings previously drifted behind the package
/// version across releases; deriving the value removes the possibility.
/// </remarks>
internal static class PlatformVersion
{
    /// <summary>
    /// Gets the reported extension version, for example <c>1.17.0</c>.
    /// </summary>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(PlatformVersion).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrEmpty(informational))
        {
            // SourceLink and CI builds append "+<commit sha>" build metadata. The platform shows this
            // string to users as an extension version, so drop the metadata and keep the semver core.
            var metadataStart = informational.IndexOf('+');
            return metadataStart < 0 ? informational : informational[..metadataStart];
        }

        // Only reachable if the attribute is stripped; the assembly version is always present.
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
