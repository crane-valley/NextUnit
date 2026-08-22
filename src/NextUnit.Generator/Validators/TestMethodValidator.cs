using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NextUnit.CodeAnalysis.Shared;
using NextUnit.Generator.Diagnostics;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Validators;

/// <summary>
/// Validates test method descriptors and reports diagnostics for common issues.
/// </summary>
internal static class TestMethodValidator
{
    /// <summary>
    /// Validates all test methods and reports diagnostics.
    /// </summary>
    public static void ValidateAll(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests)
    {
        var dependencyGraph = BuildDependencyGraph(tests);

        foreach (var test in tests)
        {
            ValidateDependencies(context, test, dependencyGraph);
            ValidateDataSourceConflicts(context, test);
            ValidateMatrixParameters(context, test);
            ValidateClassDataSources(context, test);
            ValidateCombinedParameterSources(context, test);
        }
    }

    private static Dictionary<string, HashSet<string>> BuildDependencyGraph(
        ImmutableArray<TestMethodDescriptor> tests)
    {
        var graph = new Dictionary<string, HashSet<string>>();
        foreach (var test in tests)
        {
            graph[test.Id] = new HashSet<string>(test.Dependencies);
        }
        return graph;
    }

    // The pipeline models are value objects carrying no syntax references, so no symbol location
    // is available here; every generator diagnostic is reported at Location.None.
    private static void Report(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        params object?[] messageArgs) =>
        context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, messageArgs));

    private static void ValidateDependencies(
        SourceProductionContext context,
        TestMethodDescriptor test,
        Dictionary<string, HashSet<string>> dependencyGraph)
    {
        // Check for dependency cycles
        if (HasCycle(test.Id, new HashSet<string>(), dependencyGraph))
        {
            Report(context, GeneratorDiagnosticDescriptors.DependencyCycle, test.Id);
        }

        // Check for unresolved dependencies
        foreach (var depId in test.Dependencies.Where(depId => !dependencyGraph.ContainsKey(depId)))
        {
            Report(context, GeneratorDiagnosticDescriptors.UnresolvedDependency, test.Id, depId);
        }
    }

    private static void ValidateDataSourceConflicts(SourceProductionContext context, TestMethodDescriptor test)
    {
        // [Arguments] and [TestData] conflict
        if (!test.ArgumentSets.IsDefaultOrEmpty && !test.TestDataSources.IsDefaultOrEmpty)
        {
            Report(context, GeneratorDiagnosticDescriptors.ArgumentsWithTestData, test.Id);
        }

        // [Matrix] and [Arguments] conflict
        if (!test.MatrixParameters.IsDefaultOrEmpty && !test.ArgumentSets.IsDefaultOrEmpty)
        {
            Report(context, GeneratorDiagnosticDescriptors.MatrixWithArguments, test.Id);
        }

        // [Matrix] and [TestData] conflict
        if (!test.MatrixParameters.IsDefaultOrEmpty && !test.TestDataSources.IsDefaultOrEmpty)
        {
            Report(context, GeneratorDiagnosticDescriptors.MatrixWithTestData, test.Id);
        }
    }

    private static void ValidateMatrixParameters(SourceProductionContext context, TestMethodDescriptor test)
    {
        if (test.MatrixParameters.IsDefaultOrEmpty)
        {
            return;
        }

        // All parameters must have [Matrix] if any do
        if (test.MatrixParameters.Length != test.Parameters.Length)
        {
            Report(
                context,
                GeneratorDiagnosticDescriptors.IncompleteMatrixParameters,
                test.Id,
                test.Parameters.Length,
                test.MatrixParameters.Length);
        }

        // [MatrixExclusion] parameter count validation
        if (!test.MatrixExclusions.IsDefaultOrEmpty)
        {
            foreach (var exclusion in test.MatrixExclusions.Where(e => e.Values.Length != test.MatrixParameters.Length))
            {
                Report(
                    context,
                    GeneratorDiagnosticDescriptors.MatrixExclusionValueCountMismatch,
                    test.Id,
                    exclusion.Values.Length,
                    test.MatrixParameters.Length);
            }
        }
    }

    private static void ValidateClassDataSources(SourceProductionContext context, TestMethodDescriptor test)
    {
        if (test.ClassDataSources.IsDefaultOrEmpty)
        {
            return;
        }

        // Conflict with other data source attributes
        if (!test.ArgumentSets.IsDefaultOrEmpty || !test.TestDataSources.IsDefaultOrEmpty || !test.MatrixParameters.IsDefaultOrEmpty)
        {
            Report(context, GeneratorDiagnosticDescriptors.ClassDataSourceWithOtherSources, test.Id);
        }

        // Keyed sharing requires Key. Not gated on the partition the way NU0022 now is: that rule
        // judges emitted code -- it fires only when the registry names the type, and a shadowed
        // source is named nowhere -- while this one judges the declaration, and SharedType.Keyed
        // without a Key is meaningless however the partition buckets the test. Gating it would also
        // move the error to the moment the user deletes the parameter-level source, which is when
        // they are trying to make the class source work, not when they wrote the mistake.
        foreach (var _ in test.ClassDataSources.Where(s => s.SharedType == SharedTypeConstants.Keyed && string.IsNullOrEmpty(s.Key)))
        {
            Report(context, GeneratorDiagnosticDescriptors.MissingKeyForKeyedClassDataSource, test.Id);
        }
    }

    private static void ValidateCombinedParameterSources(SourceProductionContext context, TestMethodDescriptor test)
    {
        if (test.CombinedParameterSources.IsDefaultOrEmpty)
        {
            return;
        }

        // Conflict with other data source attributes
        if (!test.ArgumentSets.IsDefaultOrEmpty || !test.TestDataSources.IsDefaultOrEmpty ||
            !test.MatrixParameters.IsDefaultOrEmpty || !test.ClassDataSources.IsDefaultOrEmpty)
        {
            Report(context, GeneratorDiagnosticDescriptors.ParameterSourcesWithOtherSources, test.Id);
        }

        // All parameters must have a data source (except trailing CancellationToken)
        var expectedSourceCount = test.Parameters.Length;
        if (test.Parameters.Length > 0 &&
            test.Parameters[test.Parameters.Length - 1].DisplayTypeName == WellKnownTypeNames.CancellationToken)
        {
            expectedSourceCount = test.Parameters.Length - 1;
        }

        if (test.CombinedParameterSources.Length != expectedSourceCount)
        {
            Report(
                context,
                GeneratorDiagnosticDescriptors.IncompleteParameterDataSources,
                test.Id,
                expectedSourceCount,
                test.CombinedParameterSources.Length);
        }

        // Keyed sharing requires Key
        foreach (var source in test.CombinedParameterSources.Where(s =>
            s.Kind == ParameterDataSourceKind.Class &&
            s.SharedType == SharedTypeConstants.Keyed &&
            string.IsNullOrEmpty(s.SharedKey)))
        {
            Report(
                context,
                GeneratorDiagnosticDescriptors.MissingKeyForKeyedValuesFrom,
                test.Id,
                source.ParameterName);
        }
    }

    private static bool HasCycle(
        string testId,
        HashSet<string> visited,
        Dictionary<string, HashSet<string>> graph)
    {
        if (!visited.Add(testId))
        {
            return true;
        }

        if (!graph.TryGetValue(testId, out var dependencies))
        {
            visited.Remove(testId);
            return false;
        }

        if (dependencies.Any(dep => HasCycle(dep, visited, graph)))
        {
            return true;
        }

        visited.Remove(testId);
        return false;
    }
}
