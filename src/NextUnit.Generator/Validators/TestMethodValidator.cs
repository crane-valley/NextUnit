using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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

    private static void ValidateDependencies(
        SourceProductionContext context,
        TestMethodDescriptor test,
        Dictionary<string, HashSet<string>> dependencyGraph)
    {
        // Check for dependency cycles
        if (HasCycle(test.Id, new HashSet<string>(), dependencyGraph))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT001",
                    "Dependency cycle detected",
                    "Test '{0}' has a circular dependency",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
        }

        // Check for unresolved dependencies
        foreach (var depId in test.Dependencies.Where(depId => !dependencyGraph.ContainsKey(depId)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT002",
                    "Unresolved test dependency",
                    "Test '{0}' depends on '{1}' which does not exist",
                    "NextUnit",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None,
                test.Id,
                depId));
        }
    }

    private static void ValidateDataSourceConflicts(SourceProductionContext context, TestMethodDescriptor test)
    {
        // [Arguments] and [TestData] conflict
        if (!test.ArgumentSets.IsDefaultOrEmpty && !test.TestDataSources.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT003",
                    "Conflicting test data attributes",
                    "Test '{0}' has both [Arguments] and [TestData] attributes. [Arguments] will be ignored and only [TestData] will be processed. Remove one of them to avoid confusion.",
                    "NextUnit",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
        }

        // [Matrix] and [Arguments] conflict
        if (!test.MatrixParameters.IsDefaultOrEmpty && !test.ArgumentSets.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT004",
                    "Conflicting test data attributes",
                    "Test '{0}' has both [Matrix] and [Arguments] attributes. Use only one approach for parameterizing tests.",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
        }

        // [Matrix] and [TestData] conflict
        if (!test.MatrixParameters.IsDefaultOrEmpty && !test.TestDataSources.IsDefaultOrEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT005",
                    "Conflicting test data attributes",
                    "Test '{0}' has both [Matrix] and [TestData] attributes. Use only one approach for parameterizing tests.",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
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
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT006",
                    "Incomplete matrix parameters",
                    "Test '{0}' has {1} parameters but only {2} have [Matrix] attributes. All parameters must have [Matrix] when using matrix tests.",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id,
                test.Parameters.Length,
                test.MatrixParameters.Length));
        }

        // [MatrixExclusion] parameter count validation
        if (!test.MatrixExclusions.IsDefaultOrEmpty)
        {
            foreach (var exclusion in test.MatrixExclusions.Where(e => e.Values.Length != test.MatrixParameters.Length))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "NEXTUNIT007",
                        "Matrix exclusion parameter count mismatch",
                        "Test '{0}' has [MatrixExclusion] with {1} values but the test has {2} matrix parameters.",
                        "NextUnit",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    test.Id,
                    exclusion.Values.Length,
                    test.MatrixParameters.Length));
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
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT008",
                    "Conflicting test data attributes",
                    "Test '{0}' has [ClassDataSource] with other data source attributes. Only [ClassDataSource] will be processed.",
                    "NextUnit",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
        }

        // Keyed sharing requires Key
        foreach (var _ in test.ClassDataSources.Where(s => s.SharedType == SharedTypeConstants.Keyed && string.IsNullOrEmpty(s.Key)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT009",
                    "Missing Key for Keyed ClassDataSource",
                    "Test '{0}' uses ClassDataSource with SharedType.Keyed but no Key is specified.",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
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
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT010",
                    "Conflicting test data attributes",
                    "Test '{0}' uses parameter-level data sources ([Values], [ValuesFromMember], [ValuesFrom]) with other data source attributes. Only parameter-level sources will be processed.",
                    "NextUnit",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None,
                test.Id));
        }

        // All parameters must have a data source (except trailing CancellationToken)
        var expectedSourceCount = test.Parameters.Length;
        if (test.Parameters.Length > 0 &&
            test.Parameters[test.Parameters.Length - 1].Type.ToDisplayString() == "System.Threading.CancellationToken")
        {
            expectedSourceCount = test.Parameters.Length - 1;
        }

        if (test.CombinedParameterSources.Length != expectedSourceCount)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT011",
                    "Incomplete parameter data sources",
                    "Test '{0}' has {1} parameters but only {2} have data source attributes ([Values], [ValuesFromMember], or [ValuesFrom]). All parameters must have a data source when using combined data sources (CancellationToken excluded).",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id,
                expectedSourceCount,
                test.CombinedParameterSources.Length));
        }

        // Keyed sharing requires Key
        foreach (var source in test.CombinedParameterSources.Where(s =>
            s.Kind == ParameterDataSourceKind.Class &&
            s.SharedType == SharedTypeConstants.Keyed &&
            string.IsNullOrEmpty(s.SharedKey)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEXTUNIT012",
                    "Missing Key for Keyed ValuesFrom",
                    "Test '{0}' uses [ValuesFrom] with SharedType.Keyed on parameter '{1}' but no Key is specified.",
                    "NextUnit",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                test.Id,
                source.ParameterName));
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
