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
    /// Builds the combined data source expander for one registry, bound to that registry's cap.
    /// </summary>
    /// <remarks>
    /// One factory rather than the same two lines in the discoverer and the executor, because the
    /// cap and the descriptors have to come from the same registry: VSTest hands the adapter one
    /// source at a time and can hold several in one process, so a run over two assemblies with
    /// different <c>&lt;NextUnitMaxTestCasesPerMethod&gt;</c> settings must bound each assembly by
    /// its own. Pairing them here is what makes that impossible to get wrong at one call site and
    /// right at the other.
    /// <para>
    /// A registry generated before the property existed reports nothing, and the built-in default
    /// applies -- the cap that build was already emitted under unless its project raised it, which
    /// is the one case an old registry cannot report and the reason this is not an error.
    /// </para>
    /// </remarks>
    public static Func<IEnumerable<CombinedDataSourceDescriptor>, IEnumerable<TestCaseDescriptor>> CreateCombinedExpander(
        Type registryType)
    {
        var registryCap = AssemblyLoader.GetStaticStructPropertyValue<int>(
            registryType, MaxTestCasesPerMethodPropertyName);

        return descriptors => CombinedDataSourceExpander.Expand(descriptors, registryCap);
    }

    /// <summary>
    /// The registry property carrying the compile-time cap, read by name like every other member the
    /// adapter reads by reflection.
    /// </summary>
    internal const string MaxTestCasesPerMethodPropertyName = "MaxTestCasesPerMethod";

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
