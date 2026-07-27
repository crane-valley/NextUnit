namespace NextUnit.Generator.Models;

internal sealed record DependencyMetadata
{
    public DependencyMetadata(
        EquatableArray<string> dependencies,
        EquatableArray<DependencyDescriptor> dependencyInfos)
    {
        Dependencies = dependencies;
        DependencyInfos = dependencyInfos;
    }

    public EquatableArray<string> Dependencies { get; }

    public EquatableArray<DependencyDescriptor> DependencyInfos { get; }
}
