using NextUnit.Shared;

namespace NextUnit.Internal;

/// <summary>
/// The cap on how many test cases one test method may expand into at discovery time.
/// </summary>
/// <remarks>
/// The number and the fallback rule come from <see cref="TestCaseExpansionPolicy"/>, which the
/// generator compiles too, so the compile-time <c>NEXTUNIT013</c> cap and this one cannot disagree.
/// What is local here is where the override is read from: parameter-level data sources
/// (<c>[Values]</c>, <c>[ValuesFromMember]</c>, <c>[ValuesFrom]</c>) resolve their values while the
/// host is starting, long after MSBuild is gone, so the run-time escape hatch is an environment
/// variable. Without the cap, three sources of a few hundred values each are enough to make
/// discovery allocate until the test host dies, before a single test runs.
/// </remarks>
internal static class TestCaseExpansionLimits
{
    /// <summary>
    /// The environment variable that overrides <see cref="TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod"/>.
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
        TestCaseExpansionPolicy.Parse(Environment.GetEnvironmentVariable(EnvironmentVariableName));
}
