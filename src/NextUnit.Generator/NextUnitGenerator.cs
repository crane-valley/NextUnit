using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NextUnit.Generator.Builders;
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
        var methodCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformCandidate(ctx))
            .Where(static candidate => candidate is not null)!;

        var testMethods = methodCandidates
            .Where(static candidate => candidate!.Test is not null)
            .Select(static (candidate, _) => candidate!.Test!);

        var lifecycleMethods = methodCandidates
            .Where(static candidate => candidate!.Lifecycle is not null)
            .Select(static (candidate, _) => candidate!.Lifecycle!);

        var testMethodsGrouped = testMethods
            .Collect()
            .Select(static (methods, _) => methods.GroupBy(m => m.FullyQualifiedTypeName).ToImmutableArray());

        var lifecycleMethodsGrouped = lifecycleMethods
            .Collect()
            .Select(static (methods, _) => methods.GroupBy(m => m.FullyQualifiedTypeName).ToImmutableArray());

        var combined = testMethodsGrouped.Combine(lifecycleMethodsGrouped);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (testGroups, lifecycleGroups) = source;
            EmitRegistry(spc, testGroups, lifecycleGroups);
        });

        var requiresEntryPoint = context.CompilationProvider
            .Select(static (compilation, cancellationToken) =>
                compilation.GetEntryPoint(cancellationToken) is null);

        context.RegisterSourceOutput(requiresEntryPoint, static (spc, shouldEmit) =>
        {
            if (shouldEmit)
            {
                EmitEntryPoint(spc);
            }
        });
    }

    private static bool IsCandidate(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static MethodCandidate? TransformCandidate(GeneratorSyntaxContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return null;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var test = TransformMethod(methodSymbol);
        var lifecycle = TransformLifecycleMethod(methodSymbol);
        return test is null && lifecycle is null
            ? null
            : new MethodCandidate(test, lifecycle);
    }

    private static TestMethodDescriptor? TransformMethod(IMethodSymbol methodSymbol)
    {
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
        var constructorKind = AttributeHelper.GetTestClassConstructorKind(typeSymbol);
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
            methodSymbol.ReturnsVoid,
            HasTrailingCancellationToken(methodSymbol),
            constructorKind,
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

    private static LifecycleMethodDescriptor? TransformLifecycleMethod(IMethodSymbol methodSymbol)
    {
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
            methodSymbol.IsStatic,
            methodSymbol.ReturnsVoid,
            HasTrailingCancellationToken(methodSymbol));
    }

    private sealed class MethodCandidate
    {
        public MethodCandidate(
            TestMethodDescriptor? test,
            LifecycleMethodDescriptor? lifecycle)
        {
            Test = test;
            Lifecycle = lifecycle;
        }

        public TestMethodDescriptor? Test { get; }

        public LifecycleMethodDescriptor? Lifecycle { get; }
    }

    private static bool HasTrailingCancellationToken(IMethodSymbol methodSymbol) =>
        methodSymbol.Parameters.Length > 0 &&
        methodSymbol.Parameters[methodSymbol.Parameters.Length - 1].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static void EmitRegistry(
        SourceProductionContext context,
        ImmutableArray<IGrouping<string, TestMethodDescriptor>> testGroups,
        ImmutableArray<IGrouping<string, LifecycleMethodDescriptor>> lifecycleGroups)
    {
        var lifecycleByType = lifecycleGroups
            .SelectMany(g => g)
            .GroupBy(l => l.FullyQualifiedTypeName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Collect global lifecycle methods (Assembly and Session scopes) from all classes
        // Only static methods are included - instance methods with Assembly/Session scope
        // don't make semantic sense as they would require creating an arbitrary instance
        var globalLifecycle = new GlobalLifecycleMethods();
        foreach (var method in lifecycleGroups.SelectMany(g => g))
        {
            // Only collect static methods for global lifecycle
            if (!method.IsStatic)
            {
                continue;
            }

            if (method.BeforeScopes.Contains(LifecycleScopeConstants.Assembly))
            {
                globalLifecycle.BeforeAssembly.Add(method);
            }

            if (method.AfterScopes.Contains(LifecycleScopeConstants.Assembly))
            {
                globalLifecycle.AfterAssembly.Add(method);
            }

            if (method.BeforeScopes.Contains(LifecycleScopeConstants.Session))
            {
                globalLifecycle.BeforeSession.Add(method);
            }

            if (method.AfterScopes.Contains(LifecycleScopeConstants.Session))
            {
                globalLifecycle.AfterSession.Add(method);
            }
        }

        var allTests = testGroups
            .SelectMany(g => g)
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        TestMethodValidator.ValidateAll(context, allTests);

        var source = GenerateSource(allTests, lifecycleByType, globalLifecycle);
        context.AddSource("GeneratedTestRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Container for global lifecycle methods (Assembly and Session scopes).
    /// </summary>
    private sealed class GlobalLifecycleMethods
    {
        public List<LifecycleMethodDescriptor> BeforeAssembly { get; } = new();
        public List<LifecycleMethodDescriptor> AfterAssembly { get; } = new();
        public List<LifecycleMethodDescriptor> BeforeSession { get; } = new();
        public List<LifecycleMethodDescriptor> AfterSession { get; } = new();
    }

    private static void EmitEntryPoint(SourceProductionContext context)
    {
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
        Dictionary<string, List<LifecycleMethodDescriptor>> lifecycleByType,
        GlobalLifecycleMethods globalLifecycle)
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
        builder.AppendLine();

        // Emit global lifecycle methods (Assembly and Session scopes)
        EmitGlobalLifecycleProperty(builder, "GlobalBeforeAssemblyMethods", globalLifecycle.BeforeAssembly);
        EmitGlobalLifecycleProperty(builder, "GlobalAfterAssemblyMethods", globalLifecycle.AfterAssembly);
        EmitGlobalLifecycleProperty(builder, "GlobalBeforeSessionMethods", globalLifecycle.BeforeSession);
        EmitGlobalLifecycleProperty(builder, "GlobalAfterSessionMethods", globalLifecycle.AfterSession);

        var reflectionRootTypes = tests
            .SelectMany(test => new[]
            {
                test.FullyQualifiedTypeName,
                test.DisplayNameFormatterType
            }
            .Concat(test.TestDataSources.Select(source => source.MemberTypeName))
            .Concat(test.ClassDataSources.Select(source => source.TypeName))
            .Concat(test.CombinedParameterSources.SelectMany(source => new[] { source.MemberTypeName, source.ClassTypeName })))
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(typeName => typeName, StringComparer.Ordinal);
        foreach (var typeName in reflectionRootTypes)
        {
            builder.AppendLine($"    [global::System.Diagnostics.CodeAnalysis.DynamicDependency(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All, typeof({typeName}))]");
        }

        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void RegisterWithTestHost()");
        builder.AppendLine("    {");
        builder.AppendLine("        global::NextUnit.Internal.GeneratedTestRegistryStore.Register(new RegistryProvider());");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private sealed class RegistryProvider : global::NextUnit.Internal.IGeneratedTestRegistry");
        builder.AppendLine("    {");
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.TestCaseDescriptor> TestCases => GeneratedTestRegistry.TestCases;");
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.TestDataDescriptor> TestDataDescriptors => GeneratedTestRegistry.TestDataDescriptors;");
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.ClassDataSourceDescriptor> ClassDataSourceDescriptors => GeneratedTestRegistry.ClassDataSourceDescriptors;");
        builder.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::NextUnit.Internal.CombinedDataSourceDescriptor> CombinedDataSourceDescriptors => GeneratedTestRegistry.CombinedDataSourceDescriptors;");
        builder.AppendLine("        public global::NextUnit.Internal.LifecycleMethodDelegate[] GlobalBeforeAssemblyMethods => GeneratedTestRegistry.GlobalBeforeAssemblyMethods;");
        builder.AppendLine("        public global::NextUnit.Internal.LifecycleMethodDelegate[] GlobalAfterAssemblyMethods => GeneratedTestRegistry.GlobalAfterAssemblyMethods;");
        builder.AppendLine("        public global::NextUnit.Internal.LifecycleMethodDelegate[] GlobalBeforeSessionMethods => GeneratedTestRegistry.GlobalBeforeSessionMethods;");
        builder.AppendLine("        public global::NextUnit.Internal.LifecycleMethodDelegate[] GlobalAfterSessionMethods => GeneratedTestRegistry.GlobalAfterSessionMethods;");
        builder.AppendLine("    }");

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void EmitGlobalLifecycleProperty(
        StringBuilder builder,
        string propertyName,
        List<LifecycleMethodDescriptor> methods)
    {
        builder.Append($"    public static global::NextUnit.Internal.LifecycleMethodDelegate[] {propertyName} {{ get; }} = ");

        if (methods.Count == 0)
        {
            builder.AppendLine("EmptyLifecycleMethods;");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("    {");
            foreach (var method in methods)
            {
                builder.AppendLine($"        {CodeBuilder.BuildLifecycleMethodDelegate(method.FullyQualifiedTypeName, method.MethodName, method.IsStatic, method.ReturnsVoid, method.AcceptsCancellationToken)},");
            }

            builder.AppendLine("    };");
        }

        builder.AppendLine();
    }
}
