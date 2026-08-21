using System.Globalization;
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
    /// <exception cref="InvalidOperationException">
    /// The environment variable is set to something other than a positive 32-bit integer.
    /// </exception>
    /// <remarks>
    /// Read on every call rather than cached: discovery resolves data sources by reflection around
    /// it, so the read costs nothing measurable, and a cached value would fix the limit at whichever
    /// moment the type happened to be initialized.
    /// </remarks>
    public static int MaxTestCasesPerMethod => Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Resolves the cap from a raw override value.
    /// </summary>
    /// <param name="rawValue">The environment variable's value, or <see langword="null"/> when unset.</param>
    /// <returns>The cap to apply.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rawValue"/> is neither unset nor a positive 32-bit integer.
    /// </exception>
    /// <remarks>
    /// Split out from the property so the refusal can be tested without setting a process-wide
    /// variable that every other test in the assembly reads concurrently.
    /// </remarks>
    internal static int Resolve(string? rawValue)
    {
        if (TestCaseExpansionPolicy.TryResolve(rawValue, out var cap))
        {
            return cap;
        }

        // Throwing rather than falling back mirrors NEXTUNIT014 on the compile-time side, for the
        // same reason: the fallback is always looser than the value that was typed, so a mistyped
        // bound meant to tighten discovery silently widened it instead. This follows
        // TestFilterConfigurationLoader, which refuses a malformed NEXTUNIT_TEST_NAME_REGEX because
        // dropping the only include filter would quietly run everything.
        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The {EnvironmentVariableName} value '{rawValue}' is not a positive 32-bit integer. Set it to a value between 1 and {int.MaxValue}, or unset it to use the default limit of {TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod}."));
    }
}
