using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NextUnit.Internal;

namespace NextUnit.TestAdapter;

/// <summary>
/// Reads the generated test registry out of a test assembly and selects its descriptors.
/// </summary>
/// <remarks>
/// The registry is reached entirely by reflection: the adapter runs in the VSTest process and
/// cannot reference the assembly under test. The registry type name and the property names are
/// therefore a runtime contract with the source generator, and a rename breaks discovery silently
/// rather than at compile time.
/// </remarks>
internal static class RegistryDescriptorReader
{
    /// <summary>
    /// Loads a source and resolves its generated registry type.
    /// </summary>
    /// <returns>
    /// The registry type, or <c>null</c> when the source could not be loaded or is not a NextUnit
    /// test assembly. Both cases are expected and neither is a failure.
    /// </returns>
    public static Type? TryResolveRegistryType(string source, IMessageLogger logger)
    {
        var loadResult = AssemblyLoader.TryLoadAssembly(source);
        if (!loadResult.Success)
        {
            AdapterDiagnostics.ReportAssemblyLoadFailure(logger, source, loadResult);
            return null;
        }

        return AssemblyLoader.GetTestRegistryType(loadResult.Assembly!);
    }

    /// <summary>
    /// Reads a descriptor list property from the registry.
    /// </summary>
    public static IReadOnlyList<TDescriptor>? ReadDescriptors<TDescriptor>(
        Type registryType,
        string propertyName) =>
        AssemblyLoader.GetStaticPropertyValue<IReadOnlyList<TDescriptor>>(registryType, propertyName);

    /// <summary>
    /// Selects the descriptors worth expanding for a run.
    /// </summary>
    /// <remarks>
    /// Filtering happens before expansion, not after: expanding a descriptor instantiates its data
    /// source, so expanding everything would run unrelated tests' data providers. When no explicit
    /// selection was made, explicit tests are excluded, matching the Platform CLI without
    /// <c>--explicit</c>.
    /// </remarks>
    public static IEnumerable<TDescriptor> SelectDescriptorsToExpand<TDescriptor>(
        IReadOnlyList<TDescriptor> descriptors,
        HashSet<string>? selectedDescriptorIds,
        Func<TDescriptor, string> baseIdSelector,
        Func<TDescriptor, bool> isExplicitSelector) =>
        selectedDescriptorIds is not null
            ? descriptors.Where(descriptor => selectedDescriptorIds.Contains(baseIdSelector(descriptor)))
            : descriptors.Where(descriptor => !isExplicitSelector(descriptor));
}
