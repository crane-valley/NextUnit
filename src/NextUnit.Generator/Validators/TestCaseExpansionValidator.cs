using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Diagnostics;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;
using NextUnit.Shared;

namespace NextUnit.Generator.Validators;

/// <summary>
/// Rejects test methods whose <c>[Matrix]</c>, <c>[Arguments]</c>, and <c>[Repeat]</c> combination
/// would expand into more test cases than the project allows.
/// </summary>
/// <remarks>
/// Every rejection is decided from the source lengths alone, before
/// <c>MatrixHelper.ComputeCartesianProduct</c> and the emission loops materialize anything: four
/// <c>[Matrix]</c> parameters, or one large <c>[Repeat]</c>, are enough to make the generator
/// allocate until the compiler dies, and a check that ran after the expansion would never be
/// reached. An expansion already known to fit inside the cap is a different matter -- there the
/// emitter's own helpers are run to count exactly what it will emit, at a cost the cap itself
/// bounds. Over-limit methods are also dropped from the registry rather than only reported, because
/// reporting alone still leaves the emitter to run the expansion this exists to prevent.
/// </remarks>
internal static class TestCaseExpansionValidator
{
    /// <summary>
    /// Reports <c>NEXTUNIT013</c> for every test method that would exceed the limit and returns the
    /// remaining methods.
    /// </summary>
    public static ImmutableArray<TestMethodDescriptor> RemoveOverLimitTests(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests,
        int maxTestCasesPerMethod)
    {
        var withinLimit = ImmutableArray.CreateBuilder<TestMethodDescriptor>(tests.Length);

        foreach (var test in tests)
        {
            var projected = ProjectTestCaseCount(test, maxTestCasesPerMethod);

            if (projected > maxTestCasesPerMethod)
            {
                // A saturated projection is reported as a bound rather than as a count, matching the
                // discovery-time message: long.MaxValue is where MultiplyClamped stops, not a number
                // of test cases anyone wrote.
                var countText = projected == long.MaxValue
                    ? $"more than {maxTestCasesPerMethod}"
                    : projected.ToString(CultureInfo.InvariantCulture);

                // Location.None matches every other generator diagnostic: the pipeline models are
                // value objects that carry no syntax reference.
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.TestCaseExpansionLimitExceeded,
                    Location.None,
                    test.Id,
                    countText,
                    maxTestCasesPerMethod));
                continue;
            }

            withinLimit.Add(test);
        }

        return withinLimit.Count == tests.Length ? tests : withinLimit.ToImmutable();
    }

    /// <summary>
    /// Projects how many test cases the registry would emit for one test method.
    /// </summary>
    private static long ProjectTestCaseCount(TestMethodDescriptor test, int maxTestCasesPerMethod)
    {
        // The bucket order mirrors RegistryEmitter's partition precedence, or a method carrying both
        // [Matrix] and [TestData] would be charged for an expansion the emitter never performs.
        if (!test.CombinedParameterSources.IsDefaultOrEmpty)
        {
            return ProjectCombinedSourceCount(test);
        }

        // [TestData] and [ClassDataSource] emit one descriptor each, so one descriptor is the whole
        // compile-time cost of them. Their rows are deliberately not capped at discovery either.
        // What this validator bounds is expansion NextUnit performs from declarative attribute data;
        // a member's rows come from running the user's own code, and capping the row count would not
        // cap the time that code takes -- a blocking member has always stalled discovery, and a cap
        // would only make the protection look wider than it is. A large row set is a supported case
        // besides: [TestData] serves one with DeferredEnumeration, which keeps discovery O(1) per
        // source, and [ClassDataSource] has no deferred mode to offer.
        if (!test.ClassDataSources.IsDefaultOrEmpty || !test.TestDataSources.IsDefaultOrEmpty)
        {
            return 1;
        }

        if (!test.MatrixParameters.IsDefaultOrEmpty)
        {
            // The peak of the running product is charged rather than its final value, because
            // MatrixHelper.ComputeCartesianProduct multiplies one parameter at a time and holds every
            // intermediate combination. An empty [Matrix()] after four 256-value ones ends at zero
            // combinations, but only after 2^32 of them have been allocated, so the final product
            // reads as "no test cases" for precisely the expansion that hangs the compiler. The peak
            // cannot over-reject the mirror case: a zero arriving first keeps every later product at
            // zero, which is the emitter doing no work at all.
            var combinations = 1L;
            var peak = 1L;

            foreach (var parameter in test.MatrixParameters)
            {
                combinations = TestCaseExpansionPolicy.MultiplyClamped(combinations, parameter.Values.Length);
                peak = Math.Max(peak, combinations);
            }

            if (peak > maxTestCasesPerMethod)
            {
                return peak;
            }

            // Past the peak check the product is known to fit inside the cap, so the emitter's own
            // expansion can be run here to get the emitted count exactly. It is run rather than
            // modelled on purpose: the emitter applies [MatrixExclusion] between building the product
            // and repeating the survivors, and every attempt to predict how many combinations an
            // exclusion removes has to re-derive matching rules that already live in MatrixHelper --
            // an exclusion naming a value no parameter offers removes none, duplicate [Matrix] values
            // let one exclusion remove several, and two identical exclusions remove one between them.
            // Calling the same helpers cannot disagree with the emitter about any of that.
            var survivors = MatrixHelper.ApplyExclusions(
                MatrixHelper.ComputeCartesianProduct(test.MatrixParameters),
                test.MatrixExclusions).Length;

            return TestCaseExpansionPolicy.ApplyRepeat(survivors, test.RepeatCount);
        }

        var argumentSetCount = test.ArgumentSets.IsDefaultOrEmpty ? 1L : test.ArgumentSets.Length;
        return TestCaseExpansionPolicy.ApplyRepeat(argumentSetCount, test.RepeatCount);
    }

    /// <summary>
    /// Projects the compile-time test case count for a method built from combined parameter sources,
    /// or a value the cap always admits when that count cannot be known until discovery.
    /// </summary>
    /// <remarks>
    /// The cap is enforced here only when the count is exact, which happens only when every combined
    /// source is inline: a <c>[Values]</c> source bakes its constants into the registry as an
    /// <c>object?[]</c> literal, so an all-inline method's product is fully known at compile time. The
    /// true product is charged -- an empty <c>[Values()]</c> zeroes it, exactly as discovery counts
    /// it -- so <c>NEXTUNIT013</c> fires only on a genuinely oversized all-inline product.
    /// <para>
    /// A runtime-resolved source (<c>[ValuesFromMember]</c>/<c>[ValuesFrom]</c>) is different: its
    /// length is unknown until the host runs it, and it can resolve to nothing, which collapses the
    /// product to zero. Charging its inline siblings as a floor -- the earlier behavior, which counted
    /// an empty <c>[Values()]</c> as one -- rejected methods whose real expansion was zero. So a
    /// combined list with any runtime source is not bounded here at all; discovery's
    /// <c>EnsureWithinExpansionLimit</c> enforces the real product once the resolved lengths are known.
    /// </para>
    /// <para>
    /// <c>[Repeat]</c> multiplies the product on both sides, through
    /// <see cref="TestCaseExpansionPolicy.ApplyRepeat"/>, so the number in <c>NEXTUNIT013</c> is the
    /// number discovery would reject the same method with. It is charged only on the all-inline
    /// branch: a runtime source that resolves to nothing zeroes the product however large the repeat
    /// count is, so charging the repeat factor on its own would reject a method that expands to no
    /// test cases at all.
    /// </para>
    /// </remarks>
    private static long ProjectCombinedSourceCount(TestMethodDescriptor test)
    {
        foreach (var source in test.CombinedParameterSources)
        {
            // A runtime-resolved source can resolve to zero rows, collapsing the whole product to
            // zero, and its length is unknown until discovery. The product therefore cannot be
            // bounded here without risking rejection of a method whose real expansion is nothing, so
            // the cap is deferred entirely to discovery. Returning 1 -- at or below every configured
            // cap, which the option parser floors at 1 -- makes RemoveOverLimitTests report nothing.
            if (source.Kind != ParameterDataSourceKind.Inline)
            {
                return 1L;
            }
        }

        // Every source is inline, so the exact product is known. The true length is charged, not
        // Math.Max(length, 1): an empty [Values()] collapses the product to zero, matching what
        // discovery counts, so an empty sibling can no longer smuggle NEXTUNIT013 onto a method whose
        // real expansion is zero.
        var inlineCombinations = 1L;

        foreach (var source in test.CombinedParameterSources)
        {
            inlineCombinations = TestCaseExpansionPolicy.MultiplyClamped(
                inlineCombinations,
                source.InlineValues.Length);
        }

        return TestCaseExpansionPolicy.ApplyRepeat(inlineCombinations, test.RepeatCount);
    }
}
