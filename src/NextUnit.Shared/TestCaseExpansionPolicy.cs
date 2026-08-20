using System.Globalization;

namespace NextUnit.Shared;

/// <summary>
/// The one definition of how many test cases a single test method may expand into, shared by the
/// compile-time cap (<c>NEXTUNIT013</c>) and the discovery-time cap.
/// </summary>
/// <remarks>
/// The cap has to be enforced twice -- the generator bounds what it emits, and discovery bounds what
/// parameter-level data sources resolve to, which is only knowable once the host is running -- but
/// the number and the rule for reading an override are one decision. A second copy of either would
/// let a build admit a method that discovery then rejects, with no single place to look for the
/// value the README documents.
/// <para>
/// Linked into <c>NextUnit.Core</c> and <c>NextUnit.Generator</c> as source rather than referenced:
/// the generator ships as a self-contained netstandard2.0 analyzer assembly and cannot take a
/// dependency on the runtime package. Reading the configured value stays on each side, because the
/// two read from different places -- an MSBuild property at compile time, an environment variable at
/// run time -- and because <c>EnforceExtendedAnalyzerRules</c> bans environment access from the
/// analyzer assembly this file is compiled into.
/// </para>
/// </remarks>
internal static class TestCaseExpansionPolicy
{
    /// <summary>
    /// The cap applied when a project or a run configures none.
    /// </summary>
    /// <remarks>
    /// High enough that no hand-written matrix reaches it, low enough that the compiler and the test
    /// host both survive the expansion. Raising it is a per-project decision, so it is a setting
    /// rather than a constant in user code.
    /// </remarks>
    public const int DefaultMaxTestCasesPerMethod = 10_000;

    /// <summary>
    /// Parses a configured cap, falling back to <see cref="DefaultMaxTestCasesPerMethod"/> for
    /// anything unusable.
    /// </summary>
    /// <param name="value">The raw configured value, or <see langword="null"/> when unset.</param>
    /// <returns>The cap to apply.</returns>
    /// <remarks>
    /// A malformed or non-positive value falls back instead of failing: the override exists to keep a
    /// build or a run alive, so a typo in it must not be the thing that stops one, and a zero or
    /// negative cap would reject every parameterized test.
    /// </remarks>
    public static int Parse(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : DefaultMaxTestCasesPerMethod;
    }

    /// <summary>
    /// Multiplies two non-negative counts, saturating at <see cref="long.MaxValue"/>.
    /// </summary>
    /// <remarks>
    /// Saturation rather than wrapping is the point: a wrapped product lands back under the cap and
    /// waves through exactly the expansion that would exhaust the compiler or the host. Four matrix
    /// parameters of 256 values are already <c>2^32</c>, which is <c>0</c> in <see cref="int"/>
    /// arithmetic, so every projection is computed in <see cref="long"/> throughout.
    /// </remarks>
    public static long MultiplyClamped(long left, long right)
    {
        if (left == 0 || right == 0)
        {
            return 0;
        }

        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }
}
