using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Diagnostics;
using NextUnit.Generator.Models;
using NextUnit.Shared;

namespace NextUnit.Generator.Validators;

/// <summary>
/// Rejects test methods whose <c>[Matrix]</c>, <c>[Arguments]</c>, and <c>[Repeat]</c> combination
/// would expand into more test cases than the project allows.
/// </summary>
/// <remarks>
/// The count is projected from the source lengths alone, before <c>MatrixHelper.ComputeCartesianProduct</c>
/// and the emission loops materialize anything: four <c>[Matrix]</c> parameters, or one large
/// <c>[Repeat]</c>, are enough to make the generator allocate until the compiler dies, and a check
/// that ran after the expansion would never be reached. Over-limit methods are also dropped from the
/// registry rather than only reported, because reporting alone still leaves the emitter to run the
/// expansion this exists to prevent.
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
            var projected = ProjectTestCaseCount(test);

            if (projected > maxTestCasesPerMethod)
            {
                // Location.None matches every other generator diagnostic: the pipeline models are
                // value objects that carry no syntax reference.
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnosticDescriptors.TestCaseExpansionLimitExceeded,
                    Location.None,
                    test.Id,
                    projected,
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
    private static long ProjectTestCaseCount(TestMethodDescriptor test)
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
        // besides, served by DeferredEnumeration, which keeps discovery O(1) per source.
        if (!test.ClassDataSources.IsDefaultOrEmpty || !test.TestDataSources.IsDefaultOrEmpty)
        {
            return 1;
        }

        var repeatCount = test.RepeatCount ?? 1;

        if (!test.MatrixParameters.IsDefaultOrEmpty)
        {
            // [MatrixExclusion] is deliberately not subtracted: the Cartesian product is materialized
            // in full and only then filtered, so the pre-exclusion size is the work being bounded.
            //
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

            return Math.Max(peak, TestCaseExpansionPolicy.MultiplyClamped(combinations, repeatCount));
        }

        var argumentSetCount = test.ArgumentSets.IsDefaultOrEmpty ? 1L : test.ArgumentSets.Length;
        return TestCaseExpansionPolicy.MultiplyClamped(argumentSetCount, repeatCount);
    }

    /// <summary>
    /// Projects the part of a combined data source expansion that is known at compile time.
    /// </summary>
    /// <remarks>
    /// Only <c>[Values]</c> is knowable here: its constants are baked into the registry as an
    /// <c>object?[]</c> literal, which the test host allocates while the registry initializes --
    /// before discovery reaches the runtime cap. Bounding the product of the inline sources bounds
    /// every one of those arrays.
    /// <para>
    /// The other kinds contribute an unknown, at-least-one factor, so the inline product is a floor
    /// on the real expansion rather than the count. A floor over the limit means the expansion is
    /// over it too, unless a source resolves to nothing at discovery -- which is why the runtime keeps
    /// its own check rather than trusting this one.
    /// </para>
    /// <para>
    /// An empty <c>[Values()]</c> counts as one rather than zero. Zeroing the product would be the
    /// truthful test case count, and it is exactly what the emitter does not do: it writes every
    /// inline array out regardless of an empty sibling, so a single empty parameter would otherwise
    /// wave through an arbitrarily large literal beside it.
    /// </para>
    /// </remarks>
    private static long ProjectCombinedSourceCount(TestMethodDescriptor test)
    {
        var inlineCombinations = 1L;

        foreach (var source in test.CombinedParameterSources)
        {
            if (source.Kind == ParameterDataSourceKind.Inline)
            {
                inlineCombinations = TestCaseExpansionPolicy.MultiplyClamped(
                    inlineCombinations,
                    Math.Max(source.InlineValues.Length, 1));
            }
        }

        return inlineCombinations;
    }
}
