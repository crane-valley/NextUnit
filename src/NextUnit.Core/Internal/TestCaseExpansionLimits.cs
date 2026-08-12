using System.Globalization;

namespace NextUnit.Internal;

/// <summary>
/// The cap on how many test cases one test method may expand into at discovery time.
/// </summary>
/// <remarks>
/// The generator applies the same cap at compile time as <c>NEXTUNIT013</c>, but parameter-level data
/// sources (<c>[Values]</c>, <c>[ValuesFromMember]</c>, <c>[ValuesFrom]</c>) resolve their values
/// while the host is starting, so their Cartesian product is only knowable here. Without the cap,
/// three sources of a few hundred values each are enough to make discovery allocate until the test
/// host dies, before a single test runs.
/// </remarks>
internal static class TestCaseExpansionLimits
{
    /// <summary>
    /// The default cap, matching the source generator's <c>NextUnitMaxTestCasesPerMethod</c> default.
    /// </summary>
    public const int DefaultMaxTestCasesPerMethod = 10_000;

    /// <summary>
    /// The environment variable that overrides <see cref="DefaultMaxTestCasesPerMethod"/>.
    /// </summary>
    public const string EnvironmentVariableName = "NEXTUNIT_MAX_TEST_CASES_PER_METHOD";

    /// <summary>
    /// Gets the cap in effect for this run.
    /// </summary>
    /// <remarks>
    /// Read on every call rather than cached: discovery resolves data sources by reflection around
    /// it, so the read costs nothing measurable, and a cached value would fix the limit at whichever
    /// moment the type happened to be initialized.
    /// </remarks>
    public static int MaxTestCasesPerMethod =>
        Parse(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Parses a configured cap, falling back to the default for anything unusable.
    /// </summary>
    /// <param name="value">The raw configured value, or <see langword="null"/> when unset.</param>
    /// <returns>The cap to apply.</returns>
    /// <remarks>
    /// A malformed or non-positive value falls back instead of failing the run: the variable exists
    /// to keep a run alive, so a typo in it must not be what stops one, and a zero or negative cap
    /// would reject every parameterized test in the assembly.
    /// </remarks>
    public static int Parse(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : DefaultMaxTestCasesPerMethod;
    }
}
