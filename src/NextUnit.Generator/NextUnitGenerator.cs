using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NextUnit.Generator.Emitters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator;

/// <summary>
/// Source generator that discovers test methods and generates test registration code for the NextUnit framework.
/// </summary>
[Generator]
public sealed class NextUnitGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the incremental source generator.
    /// </summary>
    /// <param name="context">The initialization context for the generator.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var testMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformMethod(ctx))
            .Where(static d => d is not null)!
            .Select(static (descriptor, _) => (TestMethodDescriptor)descriptor!);

        var lifecycleMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformLifecycleMethod(ctx))
            .Where(static d => d is not null)!
            .Select(static (descriptor, _) => (LifecycleMethodDescriptor)descriptor!);

        var testMethodsGrouped = testMethods
            .Collect()
            .Select(static (methods, _) => methods.GroupBy(m => m.FullyQualifiedTypeName).ToImmutableArray());

        var lifecycleMethodsGrouped = lifecycleMethods
            .Collect()
            .Select(static (methods, _) => methods.GroupBy(m => m.FullyQualifiedTypeName).ToImmutableArray());

        var combined = context.CompilationProvider
            .Combine(testMethodsGrouped)
            .Combine(lifecycleMethodsGrouped);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var ((compilation, testGroups), lifecycleGroups) = source;
            EmitRegistry(spc, compilation, testGroups, lifecycleGroups);
            EmitEntryPoint(spc, compilation);
        });
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static object? TransformMethod(GeneratorSyntaxContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        if (!AttributeHelper.HasAttribute(methodSymbol, AttributeHelper.TestAttributeMetadataName))
        {
            return null;
        }

        var typeSymbol = methodSymbol.ContainingType;
        var fullyQualifiedTypeName = AttributeHelper.GetFullyQualifiedTypeName(typeSymbol);
        var id = AttributeHelper.CreateTestId(methodSymbol);
        var customDisplayName = AttributeHelper.GetCustomDisplayName(methodSymbol);
        var displayName = customDisplayName ?? methodSymbol.Name;
        var displayNameFormatterType = AttributeHelper.GetDisplayNameFormatterType(methodSymbol, typeSymbol);
        var (notInParallel, constraintKeys) = AttributeHelper.GetNotInParallelInfo(methodSymbol, typeSymbol);
        var parallelGroup = AttributeHelper.GetParallelGroup(methodSymbol, typeSymbol);
        var methodParallelLimit = AttributeHelper.GetParallelLimit(methodSymbol);
        var typeParallelLimit = AttributeHelper.GetParallelLimit(typeSymbol);
        var parallelLimit = methodParallelLimit ?? typeParallelLimit;
        var dependencies = AttributeHelper.GetDependencies(methodSymbol);
        var dependencyInfos = AttributeHelper.GetDependencyInfos(methodSymbol);
        var (isSkipped, skipReason) = AttributeHelper.GetSkipInfo(methodSymbol);
        var argumentSets = AttributeHelper.GetArgumentSets(methodSymbol);
        var testDataSources = AttributeHelper.GetTestDataSources(methodSymbol);
        var parameters = methodSymbol.Parameters;
        var categories = AttributeHelper.GetCategories(methodSymbol, typeSymbol);
        var tags = AttributeHelper.GetTags(methodSymbol, typeSymbol);
        var requiresTestOutput = AttributeHelper.RequiresTestOutput(typeSymbol);
        var requiresTestContext = AttributeHelper.RequiresTestContext(typeSymbol);
        var timeoutMs = AttributeHelper.GetTimeout(methodSymbol, typeSymbol);
        var (retryCount, retryDelayMs, isFlaky, flakyReason) = AttributeHelper.GetRetryInfo(methodSymbol, typeSymbol);
        var repeatCount = AttributeHelper.GetRepeatCount(methodSymbol);

        return new TestMethodDescriptor(
            id,
            displayName,
            fullyQualifiedTypeName,
            methodSymbol.Name,
            notInParallel,
            constraintKeys,
            parallelGroup,
            parallelLimit,
            dependencies,
            dependencyInfos,
            isSkipped,
            skipReason,
            argumentSets,
            testDataSources,
            parameters,
            categories,
            tags,
            methodSymbol.IsStatic,
            requiresTestOutput,
            requiresTestContext,
            timeoutMs,
            retryCount,
            retryDelayMs,
            isFlaky,
            flakyReason,
            customDisplayName,
            displayNameFormatterType,
            repeatCount);
    }

    private static object? TransformLifecycleMethod(GeneratorSyntaxContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var typeSymbol = methodSymbol.ContainingType;
        var fullyQualifiedTypeName = AttributeHelper.GetFullyQualifiedTypeName(typeSymbol);

        var beforeScopes = AttributeHelper.GetLifecycleScopes(methodSymbol, AttributeHelper.BeforeAttributeMetadataName);
        var afterScopes = AttributeHelper.GetLifecycleScopes(methodSymbol, AttributeHelper.AfterAttributeMetadataName);

        if (beforeScopes.IsEmpty && afterScopes.IsEmpty)
        {
            return null;
        }

        return new LifecycleMethodDescriptor(
            fullyQualifiedTypeName,
            methodSymbol.Name,
            beforeScopes,
            afterScopes,
            methodSymbol.IsStatic);
    }

    private static void EmitRegistry(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<IGrouping<string, TestMethodDescriptor>> testGroups,
        ImmutableArray<IGrouping<string, LifecycleMethodDescriptor>> lifecycleGroups)
    {
        _ = compilation;

        var lifecycleByType = lifecycleGroups
            .SelectMany(g => g)
            .GroupBy(l => l.FullyQualifiedTypeName)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allTests = testGroups
            .SelectMany(g => g)
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        ValidateAndReportDiagnostics(context, allTests, lifecycleByType);

        var source = GenerateSource(allTests, lifecycleByType);
        context.AddSource("GeneratedTestRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static void EmitEntryPoint(SourceProductionContext context, Compilation compilation)
    {
        if (compilation.GetEntryPoint(context.CancellationToken) is not null)
        {
            return;
        }

        var source = @"// <auto-generated />
#nullable enable
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using NextUnit.Platform;

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddNextUnit();
        using var app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}";
        context.AddSource("Program.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static void ValidateAndReportDiagnostics(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests,
        Dictionary<string, List<LifecycleMethodDescriptor>> lifecycleByType)
    {
        var dependencyGraph = new Dictionary<string, HashSet<string>>();
        foreach (var test in tests)
        {
            dependencyGraph[test.Id] = new HashSet<string>(test.Dependencies);
        }

        foreach (var test in tests)
        {
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

            foreach (var depId in test.Dependencies)
            {
                if (!dependencyGraph.ContainsKey(depId))
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
        }
    }

    private static bool HasCycle(string testId, HashSet<string> visited, Dictionary<string, HashSet<string>> graph)
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

        foreach (var dep in dependencies)
        {
            if (HasCycle(dep, visited, graph))
            {
                return true;
            }
        }

        visited.Remove(testId);
        return false;
    }

    private static string GenerateSource(
        IReadOnlyList<TestMethodDescriptor> tests,
        Dictionary<string, List<LifecycleMethodDescriptor>> lifecycleByType)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine();
        builder.AppendLine("namespace NextUnit.Generated;");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedTestRegistry");
        builder.AppendLine("{");

        builder.AppendLine("    private static async Task InvokeTestMethodAsync(Action method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        method();");
        builder.AppendLine("        await Task.CompletedTask.ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async Task InvokeTestMethodAsync(Func<Task> method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        await method().ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async Task InvokeTestMethodAsync(Func<CancellationToken, Task> method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        await method(ct).ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async Task InvokeLifecycleMethodAsync(Action method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        method();");
        builder.AppendLine("        await Task.CompletedTask.ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async Task InvokeLifecycleMethodAsync(Func<Task> method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        await method().ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static async Task InvokeLifecycleMethodAsync(Func<CancellationToken, Task> method, CancellationToken ct)");
        builder.AppendLine("    {");
        builder.AppendLine("        await method(ct).ConfigureAwait(false);");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Separate tests with TestData from regular tests
        var regularTests = new List<TestMethodDescriptor>();
        var testDataTests = new List<TestMethodDescriptor>();

        foreach (var test in tests)
        {
            if (test.TestDataSources.IsDefaultOrEmpty)
            {
                regularTests.Add(test);
            }
            else
            {
                testDataTests.Add(test);
            }
        }

        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.TestCaseDescriptor> TestCases { get; } =");
        builder.AppendLine("        new global::NextUnit.Internal.TestCaseDescriptor[]");
        builder.AppendLine("        {");

        foreach (var test in regularTests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            var repeatCount = test.RepeatCount ?? 1;
            var hasRepeatAttribute = test.RepeatCount.HasValue;

            if (test.ArgumentSets.IsDefaultOrEmpty)
            {
                // No arguments - emit repeatCount test cases
                for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    // Emit repeat index if [Repeat] attribute is present (even for Repeat(1))
                    var repeatIndexToEmit = hasRepeatAttribute ? repeatIndex : (int?)null;
                    TestCaseEmitter.EmitTestCase(builder, test, lifecycleMethods, null, -1, repeatIndexToEmit);
                }
            }
            else
            {
                // With arguments - emit argumentSets.Length * repeatCount test cases
                for (var argIndex = 0; argIndex < test.ArgumentSets.Length; argIndex++)
                {
                    for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                    {
                        // Emit repeat index if [Repeat] attribute is present (even for Repeat(1))
                        var repeatIndexToEmit = hasRepeatAttribute ? repeatIndex : (int?)null;
                        TestCaseEmitter.EmitTestCase(builder, test, lifecycleMethods, test.ArgumentSets[argIndex], argIndex, repeatIndexToEmit);
                    }
                }
            }
        }

        builder.AppendLine("        };");
        builder.AppendLine();

        // Generate TestDataDescriptors for tests using [TestData]
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.TestDataDescriptor> TestDataDescriptors { get; } =");
        builder.AppendLine("        new global::NextUnit.Internal.TestDataDescriptor[]");
        builder.AppendLine("        {");

        foreach (var test in testDataTests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            foreach (var dataSource in test.TestDataSources)
            {
                TestCaseEmitter.EmitTestDataDescriptor(builder, test, lifecycleMethods, dataSource);
            }
        }

        builder.AppendLine("        };");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
