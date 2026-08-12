using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;

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
    /// The default cap on how many test cases one test method may expand into.
    /// </summary>
    /// <remarks>
    /// High enough that no hand-written matrix reaches it, low enough that the compiler survives the
    /// expansion. Raising it is a per-project decision, so it is a property rather than a constant in
    /// user code.
    /// </remarks>
    public const int DefaultMaxTestCasesPerMethod = 10_000;

    /// <summary>
    /// The analyzer config key MSBuild writes <c>NextUnitMaxTestCasesPerMethod</c> to.
    /// </summary>
    public const string MaxTestCasesPerMethodKey = "build_property.NextUnitMaxTestCasesPerMethod";

    /// <summary>
    /// Reads the configured cap on emitted test cases per test method.
    /// </summary>
    /// <param name="globalOptions">The analyzer config global options for the compilation.</param>
    /// <returns>The configured cap, or <see cref="DefaultMaxTestCasesPerMethod"/>.</returns>
    public static int ReadMaxTestCasesPerMethod(AnalyzerConfigOptions globalOptions)
    {
        if (!globalOptions.TryGetValue(MaxTestCasesPerMethodKey, out var raw))
        {
            return DefaultMaxTestCasesPerMethod;
        }

        // A malformed or non-positive value falls back to the default rather than failing the build.
        // The property exists to keep a compilation alive, so a typo in it must not be the thing that
        // stops one, and a zero or negative cap would reject every test method in the project.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : DefaultMaxTestCasesPerMethod;
    }
}
