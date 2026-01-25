using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NextUnit.Generator.Emitters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;
using NextUnit.Generator.Validators;

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
        var (isExplicit, explicitReason) = AttributeHelper.GetExplicitInfo(methodSymbol, typeSymbol);
        var argumentSets = AttributeHelper.GetArgumentSets(methodSymbol);
        var testDataSources = AttributeHelper.GetTestDataSources(methodSymbol);
        var classDataSources = AttributeHelper.GetClassDataSources(methodSymbol);
        var parameters = methodSymbol.Parameters;
        var categories = AttributeHelper.GetCategories(methodSymbol, typeSymbol);
        var tags = AttributeHelper.GetTags(methodSymbol, typeSymbol);
        var requiresTestOutput = AttributeHelper.RequiresTestOutput(typeSymbol);
        var requiresTestContext = AttributeHelper.RequiresTestContext(typeSymbol);
        var timeoutMs = AttributeHelper.GetTimeout(methodSymbol, typeSymbol);
        var (retryCount, retryDelayMs, isFlaky, flakyReason) = AttributeHelper.GetRetryInfo(methodSymbol, typeSymbol);
        var repeatCount = AttributeHelper.GetRepeatCount(methodSymbol);
        var matrixParameters = AttributeHelper.GetMatrixParameters(methodSymbol);
        var matrixExclusions = AttributeHelper.GetMatrixExclusions(methodSymbol);
        var combinedParameterSources = AttributeHelper.GetCombinedParameterSources(methodSymbol);
        var priority = AttributeHelper.GetExecutionPriority(methodSymbol, typeSymbol);

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
            isExplicit,
            explicitReason,
            argumentSets,
            testDataSources,
            classDataSources,
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
            repeatCount,
            matrixParameters,
            matrixExclusions,
            combinedParameterSources,
            priority);
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

        TestMethodValidator.ValidateAll(context, allTests);

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

    private static string GenerateSource(
        IReadOnlyList<TestMethodDescriptor> tests,
        Dictionary<string, List<LifecycleMethodDescriptor>> lifecycleByType)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#pragma warning disable");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Threading;");
        builder.AppendLine("using System.Threading.Tasks;");
        builder.AppendLine();
        builder.AppendLine("namespace NextUnit.Generated;");
        builder.AppendLine();
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"NextUnit.Generator\", \"1.0.0\")]");
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

        // Static readonly empty arrays to reduce type resolution overhead and code size
        builder.AppendLine("    private static readonly global::NextUnit.Internal.LifecycleMethodDelegate[] EmptyLifecycleMethods = [];");
        builder.AppendLine("    private static readonly string[] EmptyStrings = [];");
        builder.AppendLine("    private static readonly global::NextUnit.Internal.TestCaseId[] EmptyTestCaseIds = [];");
        builder.AppendLine("    private static readonly global::NextUnit.Internal.DependencyInfo[] EmptyDependencyInfos = [];");
        builder.AppendLine("    private static readonly global::System.Type[] EmptyTypes = [];");
        builder.AppendLine();

        // Separate tests by type: regular, matrix, TestData, ClassDataSource, and CombinedDataSource
        // Use default List<T> capacity - grows dynamically as items are added
        var regularTests = new List<TestMethodDescriptor>();
        var matrixTests = new List<TestMethodDescriptor>();
        var testDataTests = new List<TestMethodDescriptor>();
        var classDataSourceTests = new List<TestMethodDescriptor>();
        var combinedDataSourceTests = new List<TestMethodDescriptor>();

        foreach (var test in tests)
        {
            if (!test.CombinedParameterSources.IsDefaultOrEmpty)
            {
                combinedDataSourceTests.Add(test);
            }
            else if (!test.ClassDataSources.IsDefaultOrEmpty)
            {
                classDataSourceTests.Add(test);
            }
            else if (!test.TestDataSources.IsDefaultOrEmpty)
            {
                testDataTests.Add(test);
            }
            else if (!test.MatrixParameters.IsDefaultOrEmpty)
            {
                matrixTests.Add(test);
            }
            else
            {
                regularTests.Add(test);
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

        // Emit matrix test cases
        foreach (var test in matrixTests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            var repeatCount = test.RepeatCount ?? 1;
            var hasRepeatAttribute = test.RepeatCount.HasValue;

            // Compute Cartesian product and apply exclusions
            var combinations = MatrixHelper.ComputeCartesianProduct(test.MatrixParameters);
            combinations = MatrixHelper.ApplyExclusions(combinations, test.MatrixExclusions);

            for (var matrixIndex = 0; matrixIndex < combinations.Length; matrixIndex++)
            {
                for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    var repeatIndexToEmit = hasRepeatAttribute ? repeatIndex : (int?)null;
                    TestCaseEmitter.EmitMatrixTestCase(builder, test, lifecycleMethods, combinations[matrixIndex], matrixIndex, repeatIndexToEmit);
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
        builder.AppendLine();

        // Generate ClassDataSourceDescriptors for tests using [ClassDataSource<T>]
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.ClassDataSourceDescriptor> ClassDataSourceDescriptors { get; } =");
        builder.AppendLine("        new global::NextUnit.Internal.ClassDataSourceDescriptor[]");
        builder.AppendLine("        {");

        foreach (var test in classDataSourceTests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            TestCaseEmitter.EmitClassDataSourceDescriptor(builder, test, lifecycleMethods, test.ClassDataSources);
        }

        builder.AppendLine("        };");
        builder.AppendLine();

        // Generate CombinedDataSourceDescriptors for tests using parameter-level data sources
        builder.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.CombinedDataSourceDescriptor> CombinedDataSourceDescriptors { get; } =");
        builder.AppendLine("        new global::NextUnit.Internal.CombinedDataSourceDescriptor[]");
        builder.AppendLine("        {");

        foreach (var test in combinedDataSourceTests)
        {
            var lifecycleMethods = lifecycleByType.TryGetValue(test.FullyQualifiedTypeName, out var methods)
                ? methods
                : new List<LifecycleMethodDescriptor>();

            TestCaseEmitter.EmitCombinedDataSourceDescriptor(builder, test, lifecycleMethods);
        }

        builder.AppendLine("        };");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
