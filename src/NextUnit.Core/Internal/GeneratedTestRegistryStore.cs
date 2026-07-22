using System.ComponentModel;

namespace NextUnit.Internal;

/// <summary>
/// Provides direct access to source-generated test metadata.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedTestRegistry
{
    /// <summary>Gets statically expanded test cases.</summary>
    public IReadOnlyList<TestCaseDescriptor> TestCases { get; }

    /// <summary>Gets descriptors backed by method-level test data.</summary>
    public IReadOnlyList<TestDataDescriptor> TestDataDescriptors { get; }

    /// <summary>Gets descriptors backed by class data sources.</summary>
    public IReadOnlyList<ClassDataSourceDescriptor> ClassDataSourceDescriptors { get; }

    /// <summary>Gets descriptors backed by combined parameter data sources.</summary>
    public IReadOnlyList<CombinedDataSourceDescriptor> CombinedDataSourceDescriptors { get; }

    /// <summary>Gets assembly setup methods.</summary>
    public LifecycleMethodDelegate[] GlobalBeforeAssemblyMethods { get; }

    /// <summary>Gets assembly teardown methods.</summary>
    public LifecycleMethodDelegate[] GlobalAfterAssemblyMethods { get; }

    /// <summary>Gets session setup methods.</summary>
    public LifecycleMethodDelegate[] GlobalBeforeSessionMethods { get; }

    /// <summary>Gets session teardown methods.</summary>
    public LifecycleMethodDelegate[] GlobalAfterSessionMethods { get; }
}

/// <summary>
/// Connects generated test metadata to the in-process test host without runtime assembly loading.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedTestRegistryStore
{
    private static IGeneratedTestRegistry? _current;

    /// <summary>Gets the registry generated for the current test application.</summary>
    public static IGeneratedTestRegistry? Current => Volatile.Read(ref _current);

    /// <summary>Registers the metadata generated for the current test application.</summary>
    public static void Register(IGeneratedTestRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Volatile.Write(ref _current, registry);
    }
}
