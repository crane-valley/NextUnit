using System.Globalization;
using NextUnit.Shared;

namespace NextUnit.Internal;

/// <summary>
/// The cap on how many test cases one test method may expand into at discovery time.
/// </summary>
/// <remarks>
/// The number and the fallback rule come from <see cref="TestCaseExpansionPolicy"/>, which the
/// generator compiles too, so the compile-time <c>NEXTUNIT013</c> cap and this one cannot disagree.
/// What is local here is where the two configured values are read from: parameter-level data sources
/// (<c>[Values]</c>, <c>[ValuesFromMember]</c>, <c>[ValuesFrom]</c>) resolve their values while the
/// host is starting, long after MSBuild is gone, so the project's compile-time cap reaches discovery
/// through the generated registry and the per-run escape hatch is an environment variable. Without
/// the cap, three sources of a few hundred values each are enough to make discovery allocate until
/// the test host dies, before a single test runs.
/// <para>
/// Precedence, when every input is usable: the environment variable overrides the registry baseline,
/// which overrides the built-in default. Raising
/// <c>&lt;NextUnitMaxTestCasesPerMethod&gt;</c> therefore raises both caps, which is the whole point
/// of carrying it in the registry; the variable stays above it so one run can widen or narrow the
/// cap without a rebuild. An unusable value is refused wherever it is read, whatever else is set.
/// </para>
/// </remarks>
internal static class TestCaseExpansionLimits
{
    /// <summary>
    /// The environment variable that overrides the registry baseline.
    /// </summary>
    public const string EnvironmentVariableName = "NEXTUNIT_MAX_TEST_CASES_PER_METHOD";

    /// <summary>
    /// Resolves the cap in effect for one registry's descriptors.
    /// </summary>
    /// <param name="registryBaseline">
    /// The cap the generated registry carries, or <see langword="null"/> when the caller has no
    /// registry to read it from.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The environment variable is set to something other than a positive 32-bit integer, or
    /// <paramref name="registryBaseline"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Read on every call rather than cached: discovery resolves data sources by reflection around
    /// it, so the read costs nothing measurable, and a cached value would fix the limit at whichever
    /// moment the type happened to be initialized -- and the baseline is per registry, so there is
    /// no one value to cache in a host that reads several test assemblies.
    /// </remarks>
    public static int ResolveFromEnvironment(int? registryBaseline) =>
        Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName), registryBaseline);

    /// <summary>
    /// Resolves the cap from a raw override value and a registry baseline.
    /// </summary>
    /// <param name="rawValue">The environment variable's value, or <see langword="null"/> when unset.</param>
    /// <param name="registryBaseline">
    /// The cap the generated registry carries, or <see langword="null"/> when there is none to read.
    /// </param>
    /// <returns>The cap to apply.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="rawValue"/> is neither unset nor a positive 32-bit integer, or
    /// <paramref name="registryBaseline"/> is not positive.
    /// </exception>
    /// <remarks>
    /// Split out from <see cref="ResolveFromEnvironment"/> so both refusals can be tested without
    /// setting a process-wide variable that every other test in the assembly reads concurrently.
    /// </remarks>
    internal static int Resolve(string? rawValue, int? registryBaseline)
    {
        // Checked before the environment variable, not after, because each configured value is
        // judged where it is read: letting a usable override rescue a broken registry would hide the
        // contract violation for exactly the runs that set the variable. The generator cannot emit a
        // non-positive cap -- TestCaseExpansionPolicy.TryResolve refuses one and NEXTUNIT014 fails
        // the build -- so this can only be a hand-written IGeneratedTestRegistry, and substituting
        // the default for it would be the same fail-open swap NEXTUNIT014 exists to refuse. Absence
        // needs no sentinel: it arrives as null, and a registry too old to carry the member answers
        // the interface's positive default.
        if (registryBaseline is <= 0)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The generated test registry reports a test case limit of {registryBaseline}, which is not positive. A registry emitted by NextUnit cannot report this, so the registry implementation is at fault."));
        }

        var baseline = registryBaseline ?? TestCaseExpansionPolicy.DefaultMaxTestCasesPerMethod;

        if (TestCaseExpansionPolicy.TryResolve(rawValue, out var cap, baseline))
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
            $"The {EnvironmentVariableName} value '{rawValue}' is not a positive 32-bit integer. Set it to a value between 1 and {int.MaxValue}, or unset it to use the limit of {baseline} this test project was built with."));
    }
}
