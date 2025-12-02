using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
        });
    }

    /// <summary>
    /// Determines whether a syntax node is a candidate for test method discovery.
    /// </summary>
    /// <param name="node">The syntax node to evaluate.</param>
    /// <returns><c>true</c> if the node is a method with attributes; otherwise, <c>false</c>.</returns>
    private static bool IsCandidate(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    /// <summary>
    /// Transforms a syntax context into a test method descriptor.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>A test method descriptor, or null if not a test method.</returns>
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

        if (!HasAttribute(methodSymbol, TestAttributeMetadataName))
        {
            return null;
        }

        var typeSymbol = methodSymbol.ContainingType;
        var fullyQualifiedTypeName = GetFullyQualifiedTypeName(typeSymbol);
        var id = CreateTestId(methodSymbol);
        var displayName = methodSymbol.Name;
        var notInParallel = HasAttribute(methodSymbol, NotInParallelMetadataName) || HasAttribute(typeSymbol, NotInParallelMetadataName);
        var methodParallelLimit = GetParallelLimit(methodSymbol);
        var typeParallelLimit = GetParallelLimit(typeSymbol);
        var parallelLimit = methodParallelLimit ?? typeParallelLimit;
        var dependencies = GetDependencies(methodSymbol);
        var (isSkipped, skipReason) = GetSkipInfo(methodSymbol);

        return new TestMethodDescriptor(
            id,
            displayName,
            fullyQualifiedTypeName,
            methodSymbol.Name,
            notInParallel,
            parallelLimit,
            dependencies,
            isSkipped,
            skipReason);
    }

    /// <summary>
    /// Transforms a syntax context into a lifecycle method descriptor.
    /// </summary>
    /// <param name="context">The generator syntax context.</param>
    /// <returns>A lifecycle method descriptor, or null if not a lifecycle method.</returns>
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
        var fullyQualifiedTypeName = GetFullyQualifiedTypeName(typeSymbol);

        var beforeScopes = GetLifecycleScopes(methodSymbol, BeforeAttributeMetadataName);
        var afterScopes = GetLifecycleScopes(methodSymbol, AfterAttributeMetadataName);

        if (beforeScopes.IsEmpty && afterScopes.IsEmpty)
        {
            return null;
        }

        return new LifecycleMethodDescriptor(
            fullyQualifiedTypeName,
            methodSymbol.Name,
            beforeScopes,
            afterScopes);
    }

    /// <summary>
    /// Emits the test registry source code.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="testGroups">The discovered test methods grouped by type.</param>
    /// <param name="lifecycleGroups">The discovered lifecycle methods grouped by type.</param>
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

        // Validate test descriptors and emit diagnostics
        ValidateAndReportDiagnostics(context, allTests, lifecycleByType);

        var source = GenerateSource(allTests, lifecycleByType);
        context.AddSource("GeneratedTestRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static void ValidateAndReportDiagnostics(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests,
        Dictionary<string, List<LifecycleMethodDescriptor>> lifecycleByType)
    {
        // Check for dependency cycles
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

            // Check for unresolved dependencies
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
        }
    }

    private static bool HasCycle(string testId, HashSet<string> visited, Dictionary<string, HashSet<string>> graph)
    {
        if (!visited.Add(testId))
        {
            return true; // Cycle detected
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

        // Add helper methods for invoking test and lifecycle methods
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

        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.TestCaseDescriptor> TestCases { get; } =");
        builder.AppendLine("        new global::NextUnit.Internal.TestCaseDescriptor[]");
        builder.AppendLine("        {");

        foreach (var test in tests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            builder.AppendLine("            new global::NextUnit.Internal.TestCaseDescriptor");
            builder.AppendLine("            {");
            builder.AppendLine($"                Id = new global::NextUnit.Internal.TestCaseId({ToLiteral(test.Id)}),");
            builder.AppendLine($"                DisplayName = {ToLiteral(test.DisplayName)},");
            builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
            builder.AppendLine($"                MethodName = {ToLiteral(test.MethodName)},");
            builder.AppendLine($"                TestMethod = {BuildTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName)},");
            builder.AppendLine($"                Lifecycle = {BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
            builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
            builder.AppendLine("                {");
            builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
            builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
            builder.AppendLine("                },");
            builder.AppendLine($"                Dependencies = {BuildDependenciesLiteral(test.Dependencies)},");
            builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
            builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? ToLiteral(test.SkipReason) : "null")}");
            builder.AppendLine("            },");
        }

        builder.AppendLine("        };");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildTestMethodDelegate(string typeName, string methodName)
    {
        return $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeTestMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    private static string BuildLifecycleMethodDelegate(string typeName, string methodName)
    {
        return $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeLifecycleMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    private static string BuildLifecycleInfoLiteral(string typeName, List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        var beforeTestMethods = lifecycleMethods
            .Where(m => m.BeforeScopes.Contains(0)) // LifecycleScope.Test = 0
            .Select(m => m.MethodName)
            .ToList();

        var afterTestMethods = lifecycleMethods
            .Where(m => m.AfterScopes.Contains(0)) // LifecycleScope.Test = 0
            .Select(m => m.MethodName)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("new global::NextUnit.Internal.LifecycleInfo");
        builder.AppendLine("                {");
        builder.Append("                    BeforeTestMethods = ");

        if (beforeTestMethods.Count == 0)
        {
            builder.AppendLine("global::System.Array.Empty<global::NextUnit.Internal.LifecycleMethodDelegate>(),");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("                    {");
            foreach (var methodName in beforeTestMethods)
            {
                builder.AppendLine($"                        {BuildLifecycleMethodDelegate(typeName, methodName)},");
            }
            builder.AppendLine("                    },");
        }

        builder.Append("                    AfterTestMethods = ");

        if (afterTestMethods.Count == 0)
        {
            builder.AppendLine("global::System.Array.Empty<global::NextUnit.Internal.LifecycleMethodDelegate>()");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("                    {");
            foreach (var methodName in afterTestMethods)
            {
                builder.AppendLine($"                        {BuildLifecycleMethodDelegate(typeName, methodName)},");
            }
            builder.AppendLine("                    }");
        }

        builder.Append("                }");
        return builder.ToString();
    }

    private static string BuildDependenciesLiteral(ImmutableArray<string> dependencies)
    {
        if (dependencies.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::NextUnit.Internal.TestCaseId>()";
        }

        var builder = new StringBuilder();
        builder.Append("new global::NextUnit.Internal.TestCaseId[] { ");

        for (var i = 0; i < dependencies.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"new global::NextUnit.Internal.TestCaseId({ToLiteral(dependencies[i])})");
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static ImmutableArray<string> GetDependencies(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, DependsOnMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];

            if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (var value in argument.Values)
                {
                    if (value.Value is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        builder.Add($"{typeName}.{name}");
                    }
                }
            }
            else if (argument.Value is string singleName && !string.IsNullOrWhiteSpace(singleName))
            {
                builder.Add($"{typeName}.{singleName}");
            }
        }

        return builder.ToImmutable();
    }

    private static (bool isSkipped, string? skipReason) GetSkipInfo(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, SkipAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                return (true, null);
            }

            var reasonArg = attribute.ConstructorArguments[0];
            if (reasonArg.Value is string reason)
            {
                return (true, reason);
            }

            return (true, null);
        }

        return (false, null);
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute, metadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == metadataName;
    }

    private static int? GetParallelLimit(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, ParallelLimitMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var value = attribute.ConstructorArguments[0].Value;

            if (value is int limit)
            {
                return limit;
            }
        }

        return null;
    }

    private static string CreateTestId(IMethodSymbol methodSymbol)
    {
        var typeName = methodSymbol.ContainingType.ToDisplayString(TestIdTypeFormat);
        return $"{typeName}.{methodSymbol.Name}";
    }

    private static string GetFullyQualifiedTypeName(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    private static string ToLiteral(string value)
    {
        return SymbolDisplay.FormatLiteral(value, true);
    }

    private static ImmutableArray<int> GetLifecycleScopes(IMethodSymbol methodSymbol, string attributeMetadataName)
    {
        var builder = ImmutableArray.CreateBuilder<int>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, attributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var value = attribute.ConstructorArguments[0].Value;

            if (value is int scope)
            {
                builder.Add(scope);
            }
        }

        return builder.ToImmutable();
    }

    private const string TestAttributeMetadataName = "global::NextUnit.TestAttribute";
    private const string BeforeAttributeMetadataName = "global::NextUnit.BeforeAttribute";
    private const string AfterAttributeMetadataName = "global::NextUnit.AfterAttribute";
    private const string NotInParallelMetadataName = "global::NextUnit.NotInParallelAttribute";
    private const string ParallelLimitMetadataName = "global::NextUnit.ParallelLimitAttribute";
    private const string DependsOnMetadataName = "global::NextUnit.DependsOnAttribute";
    private const string SkipAttributeMetadataName = "global::NextUnit.SkipAttribute";

    private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat TestIdTypeFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private sealed class TestMethodDescriptor
    {
        public TestMethodDescriptor(
            string id,
            string displayName,
            string fullyQualifiedTypeName,
            string methodName,
            bool notInParallel,
            int? parallelLimit,
            ImmutableArray<string> dependencies,
            bool isSkipped,
            string? skipReason)
        {
            Id = id;
            DisplayName = displayName;
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            MethodName = methodName;
            NotInParallel = notInParallel;
            ParallelLimit = parallelLimit;
            Dependencies = dependencies;
            IsSkipped = isSkipped;
            SkipReason = skipReason;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string FullyQualifiedTypeName { get; }
        public string MethodName { get; }
        public bool NotInParallel { get; }
        public int? ParallelLimit { get; }
        public ImmutableArray<string> Dependencies { get; }
        public bool IsSkipped { get; }
        public string? SkipReason { get; }
    }

    private sealed class LifecycleMethodDescriptor
    {
        public LifecycleMethodDescriptor(
            string fullyQualifiedTypeName,
            string methodName,
            ImmutableArray<int> beforeScopes,
            ImmutableArray<int> afterScopes)
        {
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            MethodName = methodName;
            BeforeScopes = beforeScopes;
            AfterScopes = afterScopes;
        }

        public string FullyQualifiedTypeName { get; }
        public string MethodName { get; }
        public ImmutableArray<int> BeforeScopes { get; }
        public ImmutableArray<int> AfterScopes { get; }
    }
}
