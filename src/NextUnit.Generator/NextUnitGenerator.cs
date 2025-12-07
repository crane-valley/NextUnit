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
        var argumentSets = GetArgumentSets(methodSymbol);
        var testDataSources = GetTestDataSources(methodSymbol);
        var parameters = methodSymbol.Parameters;
        var categories = GetCategories(methodSymbol, typeSymbol);
        var tags = GetTags(methodSymbol, typeSymbol);

        return new TestMethodDescriptor(
            id,
            displayName,
            fullyQualifiedTypeName,
            methodSymbol.Name,
            notInParallel,
            parallelLimit,
            dependencies,
            isSkipped,
            skipReason,
            argumentSets,
            testDataSources,
            parameters,
            categories,
            tags,
            methodSymbol.IsStatic);
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

            // Warn when both [Arguments] and [TestData] are used on the same method
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

            if (test.ArgumentSets.IsDefaultOrEmpty)
            {
                EmitTestCase(builder, test, lifecycleMethods, null, -1);
            }
            else
            {
                for (var i = 0; i < test.ArgumentSets.Length; i++)
                {
                    EmitTestCase(builder, test, lifecycleMethods, test.ArgumentSets[i], i);
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
                EmitTestDataDescriptor(builder, test, lifecycleMethods, dataSource);
            }
        }

        builder.AppendLine("        };");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void EmitTestDataDescriptor(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        TestDataSource dataSource)
    {
        var dataSourceType = dataSource.MemberTypeName ?? test.FullyQualifiedTypeName;

        builder.AppendLine("            new global::NextUnit.Internal.TestDataDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                BaseId = {ToLiteral(test.Id)},");
        builder.AppendLine($"                DisplayName = {ToLiteral(test.DisplayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {ToLiteral(test.MethodName)},");
        builder.AppendLine($"                DataSourceName = {ToLiteral(dataSource.MemberName)},");
        builder.AppendLine($"                DataSourceType = typeof({dataSourceType}),");
        builder.AppendLine($"                ParameterTypes = {BuildParameterTypesLiteral(test.Parameters)},");
        builder.AppendLine($"                Lifecycle = {BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                Categories = {BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {BuildStringArrayLiteral(test.Tags)}");
        builder.AppendLine("            },");
    }

    private static void EmitTestCase(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        ImmutableArray<TypedConstant>? arguments,
        int argumentSetIndex)
    {
        var testId = test.Id;
        var displayName = test.DisplayName;

        if (arguments.HasValue)
        {
            testId = $"{test.Id}[{argumentSetIndex}]";
            displayName = BuildParameterizedDisplayName(test.DisplayName, arguments.Value);
        }

        builder.AppendLine("            new global::NextUnit.Internal.TestCaseDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                Id = new global::NextUnit.Internal.TestCaseId({ToLiteral(testId)}),");
        builder.AppendLine($"                DisplayName = {ToLiteral(displayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {ToLiteral(test.MethodName)},");

        if (arguments.HasValue)
        {
            builder.AppendLine($"                TestMethod = {BuildParameterizedTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.Parameters, arguments.Value, test.IsStatic)},");
        }
        else
        {
            builder.AppendLine($"                TestMethod = {BuildTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.IsStatic)},");
        }

        builder.AppendLine($"                Lifecycle = {BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? ToLiteral(test.SkipReason) : "null")},");

        if (arguments.HasValue)
        {
            builder.AppendLine($"                Arguments = {BuildArgumentsLiteral(arguments.Value)},");
        }
        else
        {
            builder.AppendLine("                Arguments = null,");
        }

        builder.AppendLine($"                Categories = {BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {BuildStringArrayLiteral(test.Tags)}");

        builder.AppendLine("            },");
    }

    private static string BuildParameterizedDisplayName(string methodName, ImmutableArray<TypedConstant> arguments)
    {
        var argsBuilder = new StringBuilder();
        argsBuilder.Append(methodName);
        argsBuilder.Append('(');

        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                argsBuilder.Append(", ");
            }

            argsBuilder.Append(FormatArgumentForDisplay(arguments[i]));
        }

        argsBuilder.Append(')');
        return argsBuilder.ToString();
    }

    private static string FormatArgumentForDisplay(TypedConstant argument)
    {
        if (argument.IsNull)
        {
            return "null";
        }

        return argument.Kind switch
        {
            TypedConstantKind.Primitive => FormatPrimitiveForDisplay(argument.Value!),
            TypedConstantKind.Enum => $"{argument.Type!.Name}.{argument.Value}",
            TypedConstantKind.Type => $"typeof({((ITypeSymbol)argument.Value!).Name})",
            TypedConstantKind.Array => FormatArrayForDisplay(argument),
            _ => argument.Value?.ToString() ?? "null"
        };
    }

    private static string FormatPrimitiveForDisplay(object value)
    {
        return value switch
        {
            string str => $"\"{str}\"",
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null"
        };
    }

    private static string FormatArrayForDisplay(TypedConstant argument)
    {
        var elements = argument.Values;

        if (elements.IsEmpty)
        {
            return "[]";
        }

        var builder = new StringBuilder();
        builder.Append('[');

        for (var i = 0; i < Math.Min(elements.Length, 3); i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(FormatArgumentForDisplay(elements[i]));
        }

        if (elements.Length > 3)
        {
            builder.Append(", ...");
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static string BuildTestMethodDelegate(string typeName, string methodName, bool isStatic)
    {
        // Static methods don't use the instance parameter.
        // Instance methods need to cast the instance parameter to the correct type.
        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeTestMethodAsync({typeName}.{methodName}, ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeTestMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    private static string BuildParameterizedTestMethodDelegate(
        string typeName,
        string methodName,
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<TypedConstant> arguments,
        bool isStatic)
    {
        var argsBuilder = new StringBuilder();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                argsBuilder.Append(", ");
            }

            var arg = arguments[i];
            var param = i < parameters.Length ? parameters[i] : null;

            argsBuilder.Append(FormatArgumentValue(arg, param?.Type));
        }

        // Static methods don't use the instance parameter; instance methods need to cast the instance.
        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeTestMethodAsync(() => {typeName}.{methodName}({argsBuilder}), ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeTestMethodAsync(() => typedInstance.{methodName}({argsBuilder}), ct).ConfigureAwait(false); }}";
    }

    private static string FormatArgumentValue(TypedConstant argument, ITypeSymbol? targetType)
    {
        if (argument.IsNull)
        {
            if (targetType != null && targetType.IsValueType)
            {
                return $"default({targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
            }
            return "null";
        }

        return argument.Kind switch
        {
            TypedConstantKind.Primitive => FormatPrimitiveValue(argument.Value!, argument.Type!),
            TypedConstantKind.Enum => $"({argument.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){argument.Value}",
            TypedConstantKind.Type => $"typeof({((ITypeSymbol)argument.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})",
            TypedConstantKind.Array => FormatArrayValue(argument),
            _ => "null"
        };
    }

    private static string FormatPrimitiveValue(object value, ITypeSymbol type)
    {
        return value switch
        {
            string str => ToLiteral(str),
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            byte or sbyte or short or ushort or int or uint => value.ToString()!,
            long l => $"{l}L",
            ulong ul => $"{ul}UL",
            float f => $"{f.ToString(CultureInfo.InvariantCulture)}f",
            double d => $"{d.ToString(CultureInfo.InvariantCulture)}d",
            decimal m => $"{m.ToString(CultureInfo.InvariantCulture)}m",
            _ => value.ToString() ?? "null"
        };
    }

    private static string FormatArrayValue(TypedConstant argument)
    {
        var elementType = ((IArrayTypeSymbol)argument.Type!).ElementType;
        var elements = argument.Values;

        if (elements.IsEmpty)
        {
            return $"global::System.Array.Empty<{elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()";
        }

        var builder = new StringBuilder();
        builder.Append($"new {elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}[] {{ ");

        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(FormatArgumentValue(elements[i], elementType));
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static string BuildArgumentsLiteral(ImmutableArray<TypedConstant> arguments)
    {
        if (arguments.IsEmpty)
        {
            return "global::System.Array.Empty<object?>()";
        }

        var builder = new StringBuilder();
        builder.Append("new object?[] { ");

        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var arg = arguments[i];
            if (arg.IsNull)
            {
                builder.Append("null");
            }
            else
            {
                builder.Append(FormatArgumentValue(arg, null));
            }
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static string BuildLifecycleMethodDelegate(string typeName, string methodName, bool isStatic)
    {
        // Static methods don't use the instance parameter; instance methods need to cast the instance.
        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeLifecycleMethodAsync({typeName}.{methodName}, ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeLifecycleMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    private static string BuildLifecycleInfoLiteral(string typeName, List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        var beforeTest = lifecycleMethods.Where(m => m.BeforeScopes.Contains(0)).ToList();
        var afterTest = lifecycleMethods.Where(m => m.AfterScopes.Contains(0)).ToList();
        var beforeClass = lifecycleMethods.Where(m => m.BeforeScopes.Contains(1)).ToList();
        var afterClass = lifecycleMethods.Where(m => m.AfterScopes.Contains(1)).ToList();
        var beforeAssembly = lifecycleMethods.Where(m => m.BeforeScopes.Contains(2)).ToList();
        var afterAssembly = lifecycleMethods.Where(m => m.AfterScopes.Contains(2)).ToList();
        var beforeSession = lifecycleMethods.Where(m => m.BeforeScopes.Contains(3)).ToList();
        var afterSession = lifecycleMethods.Where(m => m.AfterScopes.Contains(3)).ToList();

        var builder = new StringBuilder();
        builder.AppendLine("new global::NextUnit.Internal.LifecycleInfo");
        builder.AppendLine("                {");

        builder.Append("                    BeforeTestMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeTest);
        builder.AppendLine(",");

        builder.Append("                    AfterTestMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterTest);
        builder.AppendLine(",");

        builder.Append("                    BeforeClassMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeClass);
        builder.AppendLine(",");

        builder.Append("                    AfterClassMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterClass);
        builder.AppendLine(",");

        builder.Append("                    BeforeAssemblyMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeAssembly);
        builder.AppendLine(",");

        builder.Append("                    AfterAssemblyMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterAssembly);
        builder.AppendLine(",");

        builder.Append("                    BeforeSessionMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeSession);
        builder.AppendLine(",");

        builder.Append("                    AfterSessionMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterSession);
        builder.AppendLine();

        builder.Append("                }");
        return builder.ToString();
    }

    private static void AppendLifecycleMethodArray(StringBuilder builder, string typeName, List<LifecycleMethodDescriptor> methods)
    {
        if (methods.Count == 0)
        {
            builder.Append("global::System.Array.Empty<global::NextUnit.Internal.LifecycleMethodDelegate>()");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("                    {");
            foreach (var method in methods)
            {
                builder.AppendLine($"                        {BuildLifecycleMethodDelegate(typeName, method.MethodName, method.IsStatic)},");
            }
            builder.Append("                    }");
        }
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

    private static string BuildStringArrayLiteral(ImmutableArray<string> strings)
    {
        if (strings.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<string>()";
        }

        var builder = new StringBuilder();
        builder.Append("new string[] { ");

        for (var i = 0; i < strings.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(ToLiteral(strings[i]));
        }

        builder.Append(" }");
        return builder.ToString();
    }

    private static string BuildParameterTypesLiteral(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::System.Type>()";
        }

        var builder = new StringBuilder();
        builder.Append("new global::System.Type[] { ");

        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"typeof({parameters[i].Type.ToDisplayString(FullyQualifiedTypeFormat)})");
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
                        // Support both short method names (from nameof) and fully qualified names
                        var dependencyId = name.Contains('.') ? name : $"{typeName}.{name}";
                        builder.Add(dependencyId);
                    }
                }
            }
            else if (argument.Value is string singleName && !string.IsNullOrWhiteSpace(singleName))
            {
                // Support both short method names (from nameof) and fully qualified names
                var dependencyId = singleName.Contains('.') ? singleName : $"{typeName}.{singleName}";
                builder.Add(dependencyId);
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

    private static ImmutableArray<ImmutableArray<TypedConstant>> GetArgumentSets(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<ImmutableArray<TypedConstant>>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, ArgumentsAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var argsArray = attribute.ConstructorArguments[0];
            if (argsArray.Kind == TypedConstantKind.Array)
            {
                builder.Add(argsArray.Values);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TestDataSource> GetTestDataSources(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<TestDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, TestDataAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not string memberName ||
                string.IsNullOrEmpty(memberName))
            {
                continue;
            }

            // Check for MemberType named argument
            var memberTypeArg = attribute.NamedArguments
                .Where(arg => arg.Key == "MemberType" && arg.Value.Value is INamedTypeSymbol)
                .Select(arg => (INamedTypeSymbol)arg.Value.Value!)
                .FirstOrDefault();

            string? memberTypeName = memberTypeArg?.ToDisplayString(FullyQualifiedTypeFormat);

            builder.Add(new TestDataSource(memberName, memberTypeName));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> GetCategories(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        // Get categories from method
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, CategoryAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string categoryName &&
                !string.IsNullOrWhiteSpace(categoryName))
            {
                builder.Add(categoryName);
            }
        }

        // Get categories from class
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, CategoryAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string categoryName &&
                !string.IsNullOrWhiteSpace(categoryName))
            {
                builder.Add(categoryName);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> GetTags(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        // Get tags from method
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, TagAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string tagName &&
                !string.IsNullOrWhiteSpace(tagName))
            {
                builder.Add(tagName);
            }
        }

        // Get tags from class
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, TagAttributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string tagName &&
                !string.IsNullOrWhiteSpace(tagName))
            {
                builder.Add(tagName);
            }
        }

        return builder.ToImmutable();
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));
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
    private const string ArgumentsAttributeMetadataName = "global::NextUnit.ArgumentsAttribute";
    private const string TestDataAttributeMetadataName = "global::NextUnit.TestDataAttribute";
    private const string CategoryAttributeMetadataName = "global::NextUnit.CategoryAttribute";
    private const string TagAttributeMetadataName = "global::NextUnit.TagAttribute";

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
            string? skipReason,
            ImmutableArray<ImmutableArray<TypedConstant>> argumentSets,
            ImmutableArray<TestDataSource> testDataSources,
            ImmutableArray<IParameterSymbol> parameters,
            ImmutableArray<string> categories,
            ImmutableArray<string> tags,
            bool isStatic)
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
            ArgumentSets = argumentSets;
            TestDataSources = testDataSources;
            Parameters = parameters;
            Categories = categories;
            Tags = tags;
            IsStatic = isStatic;
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
        public ImmutableArray<ImmutableArray<TypedConstant>> ArgumentSets { get; }
        public ImmutableArray<TestDataSource> TestDataSources { get; }
        public ImmutableArray<IParameterSymbol> Parameters { get; }
        public ImmutableArray<string> Categories { get; }
        public ImmutableArray<string> Tags { get; }
        public bool IsStatic { get; }
    }

    private sealed class LifecycleMethodDescriptor
    {
        public LifecycleMethodDescriptor(
            string fullyQualifiedTypeName,
            string methodName,
            ImmutableArray<int> beforeScopes,
            ImmutableArray<int> afterScopes,
            bool isStatic)
        {
            FullyQualifiedTypeName = fullyQualifiedTypeName;
            MethodName = methodName;
            BeforeScopes = beforeScopes;
            AfterScopes = afterScopes;
            IsStatic = isStatic;
        }

        public string FullyQualifiedTypeName { get; }
        public string MethodName { get; }
        public ImmutableArray<int> BeforeScopes { get; }
        public ImmutableArray<int> AfterScopes { get; }
        public bool IsStatic { get; }
    }

    private sealed class TestDataSource
    {
        public TestDataSource(string memberName, string? memberTypeName)
        {
            MemberName = memberName;
            MemberTypeName = memberTypeName;
        }

        public string MemberName { get; }
        public string? MemberTypeName { get; }
    }
}
