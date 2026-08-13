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
    /// The configured cap, or <see cref="TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod"/> when
    /// the property is unset or unusable.
    /// </returns>
    public static int ReadMaxTestCasesPerMethod(AnalyzerConfigOptions globalOptions)
    {
        // An unset property and an unusable one take the same path: TryGetValue leaves the value null
        // on a miss, and Parse already treats null as unset.
        _ = globalOptions.TryGetValue(MaxTestCasesPerMethodKey, out var raw);

        return TestCaseExpansionPolicy.Parse(raw);
    }
}
