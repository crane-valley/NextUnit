using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Resolves test case descriptors for a given assembly.
/// </summary>
/// <remarks>
/// ?? DEVELOPMENT FALLBACK: Currently uses reflection.
/// TODO: Replace with generator-only approach before v1.0.
/// </remarks>
public static class TestDescriptorProvider
{
    /// <summary>
    /// Gets the test case descriptors declared in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>A read-only list of <see cref="TestCaseDescriptor"/> entries.</returns>
    public static IReadOnlyList<TestCaseDescriptor> GetTestCases(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        // TODO M1: Replace with generator-only approach
        // For now, using reflection fallback for development
        return ReflectionTestDescriptorBuilder.Build(assembly);
    }
}
