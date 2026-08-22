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
    /// Resolves a configured cap, separating an override that is unset from one that is present but
    /// unusable.
    /// </summary>
    /// <param name="value">The raw configured value, or <see langword="null"/> when unset.</param>
    /// <param name="cap">
    /// The cap to apply: the configured value when it is usable, and
    /// <see cref="DefaultMaxTestCasesPerMethod"/> when the override is unset or unusable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the override is unset or usable, and <see langword="false"/> when
    /// it is present but unusable.
    /// </returns>
    /// <remarks>
    /// Blank reads as unset rather than unusable, and that is not a stylistic choice: MSBuild writes
    /// every <c>CompilerVisibleProperty</c> into the generated analyzer config whether or not the
    /// project defines it, so the generator is handed
    /// <c>build_property.NextUnitMaxTestCasesPerMethod =</c> with an empty value by every project
    /// that never set it. Refusing blank would fail the build of every consumer that left the escape
    /// hatch alone. Whitespace follows blank because the analyzer config parser trims a value before
    /// the generator sees it, so the two are not distinguishable at this end anyway.
    /// <para>
    /// Anything else that is not a positive <see cref="int"/> is refused rather than ignored. Falling
    /// back kept a build alive through a typo, but it did so on a security bound and only ever in the
    /// loosening direction -- <c>100O</c> written for <c>1000</c> silently granted the 10000 default
    /// -- and it left the caller no way to tell an unset override from a rejected one. Refusing here
    /// and reporting at each call site is what makes the two distinguishable.
    /// </para>
    /// </remarks>
    public static bool TryResolve(string? value, out int cap)
    {
        cap = DefaultMaxTestCasesPerMethod;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            cap = parsed;
            return true;
        }

        return false;
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

    /// <summary>
    /// Multiplies an expansion by the factor a <c>[Repeat]</c> attribute contributes to it.
    /// </summary>
    /// <param name="count">The expansion before the attribute is applied.</param>
    /// <param name="repeatCount">
    /// The declared repeat count, or <see langword="null"/> when the method carries no <c>[Repeat]</c>.
    /// </param>
    /// <remarks>
    /// The factor lives beside the cap it is charged against because it is charged in two places that
    /// must agree: <c>TestCaseExpansionValidator</c> projects it at compile time, and
    /// <c>CombinedDataSourceExpander</c> charges it again once the runtime sources are resolved. A
    /// second <c>?? 1</c> written at either site is exactly the shape that lets a build admit a method
    /// discovery then rejects, with two different numbers in the two messages.
    /// <para>
    /// An absent attribute contributes one rather than zero, so a method without <c>[Repeat]</c>
    /// expands to what it always did. A declared count below one is unreachable:
    /// <c>RepeatAttribute</c> refuses it at construction.
    /// </para>
    /// </remarks>
    public static long ApplyRepeat(long count, int? repeatCount) => MultiplyClamped(count, repeatCount ?? 1);
}
