using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.Shared;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Reads the MSBuild properties the generator honors from the analyzer config global options.
/// </summary>
/// <remarks>
/// The properties reach here only because the shipped <c>NextUnit.props</c> lists them as
/// <c>CompilerVisibleProperty</c>; a project that references the generator without the package sees
/// the defaults.
/// </remarks>
internal static class GeneratorOptions
{
    /// <summary>
    /// The analyzer config key MSBuild writes <c>NextUnitMaxTestCasesPerMethod</c> to.
    /// </summary>
    public const string MaxTestCasesPerMethodKey = "build_property.NextUnitMaxTestCasesPerMethod";

    /// <summary>
    /// Reads the configured cap on emitted test cases per test method.
    /// </summary>
    /// <param name="globalOptions">The analyzer config global options for the compilation.</param>
    /// <returns>
    /// The cap to apply, and the raw property value when one was set but is unusable
    /// (<see langword="null"/> when the property is unset or usable).
    /// </returns>
    /// <remarks>
    /// A tuple rather than a record: this value enters the incremental pipeline, where it is compared
    /// against the previous run to decide whether the source output can be reused, so it has to carry
    /// structural equality -- and the generator targets netstandard2.0, which has no
    /// <c>IsExternalInit</c> for a record's synthesized init accessors. Returning the rejected value
    /// rather than reporting it here keeps the reading side free of a <c>SourceProductionContext</c>
    /// it would hold for one diagnostic.
    /// </remarks>
    public static (int Cap, string? UnusableValue) ReadMaxTestCasesPerMethod(AnalyzerConfigOptions globalOptions)
    {
        // TryGetValue leaves the value null on a miss, and TryResolve already reads null as unset.
        _ = globalOptions.TryGetValue(MaxTestCasesPerMethodKey, out var raw);

        return TestCaseExpansionPolicy.TryResolve(raw, out var cap) ? (cap, null) : (cap, raw);
    }
}
