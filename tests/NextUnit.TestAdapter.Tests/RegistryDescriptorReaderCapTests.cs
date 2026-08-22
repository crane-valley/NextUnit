using NextUnit.Internal;
using NextUnit.Shared;
using Xunit;

namespace NextUnit.TestAdapter.Tests;

/// <summary>
/// Pins the reflection half of the expansion cap contract: VSTest hands the adapter one source
/// assembly at a time and can hold several in one process, so the cap has to come from the same
/// registry type the descriptors did.
/// </summary>
/// <remarks>
/// Reading it from <c>GeneratedTestRegistryStore.Current</c> instead would take whichever module
/// initializer ran last, and one assembly's tests would be bounded by another assembly's
/// <c>&lt;NextUnitMaxTestCasesPerMethod&gt;</c>.
/// </remarks>
public sealed class RegistryDescriptorReaderCapTests
{
    [Fact]
    public void CreateCombinedExpander_BoundsEachRegistryByItsOwnCap()
    {
        var descriptor = CreateDescriptor(valuesPerParameter: 40);

        var generous = RegistryDescriptorReader.CreateCombinedExpander(typeof(GenerousRegistry));
        var strict = RegistryDescriptorReader.CreateCombinedExpander(typeof(StrictRegistry));

        Assert.Equal(40, generous([descriptor]).Count());
        Assert.Throws<InvalidOperationException>(() => strict([descriptor]).ToList());
    }

    /// <summary>
    /// A registry generated before the property existed reports nothing, and the built-in default
    /// applies rather than the read failing.
    /// </summary>
    [Fact]
    public void CreateCombinedExpander_RegistryWithoutTheProperty_UsesTheBuiltInDefault()
    {
        var descriptor = CreateDescriptor(valuesPerParameter: 40);
        var expander = RegistryDescriptorReader.CreateCombinedExpander(typeof(RegistryPredatingTheCap));

        Assert.Equal(40, expander([descriptor]).Count());
        Assert.True(TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod > 40);
    }

    private static CombinedDataSourceDescriptor CreateDescriptor(int valuesPerParameter) => new()
    {
        BaseId = "Tests.AdapterCap.Combined",
        TestClass = typeof(CombinedTarget),
        MethodName = nameof(CombinedTarget.Run),
        ParameterTypes = [typeof(int)],
        ParameterSources =
        [
            new ParameterDataSource
            {
                ParameterIndex = 0,
                ParameterName = "only",
                Kind = ParameterDataSourceKind.Inline,
                InlineValues = Enumerable.Range(0, valuesPerParameter).Cast<object?>().ToArray(),
            }
        ],
    };

    private sealed class CombinedTarget
    {
        public void Run(int only) => _ = only;
    }

    // Shaped like the generated registry's static surface, because that is what the adapter reads:
    // a renamed property here fails these tests the way a renamed one in the emitter would.
    private static class GenerousRegistry
    {
        public static int MaxTestCasesPerMethod { get; } = 40;
    }

    private static class StrictRegistry
    {
        public static int MaxTestCasesPerMethod { get; } = 39;
    }

    private static class RegistryPredatingTheCap
    {
    }
}
