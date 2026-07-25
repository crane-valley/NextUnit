using Microsoft.CodeAnalysis.Testing;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Reference assemblies shared by the generator test helpers.
/// </summary>
internal static class TestReferenceAssemblies
{
    private static readonly Lazy<ReferenceAssemblies> _net10 = new(CreateNet10);

    /// <summary>
    /// Gets the net10.0 reference assemblies, matching the product target framework.
    /// </summary>
    public static ReferenceAssemblies Net10 => _net10.Value;

    private static ReferenceAssemblies CreateNet10()
    {
        return new ReferenceAssemblies(
            "net10.0",
            new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
            Path.Combine("ref", "net10.0"));
    }
}
