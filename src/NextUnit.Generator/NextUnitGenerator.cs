using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NextUnit.CodeAnalysis.Shared;
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
            .ForAttributeWithMetadataName(
                NextUnitAttributeNames.Test,
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformTestMethod(ctx))
            .Where(static test => test is not null)
            .Select(static (test, _) => test!);

        var beforeMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NextUnitAttributeNames.Before,
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformLifecycleMethod(ctx, isBefore: true))
            .Where(static method => method is not null)
            .Select(static (method, _) => method!);

        var afterMethods = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NextUnitAttributeNames.After,
                predicate: static (node, _) => IsCandidate(node),
                transform: static (ctx, _) => TransformLifecycleMethod(ctx, isBefore: false))
            .Where(static method => method is not null)
            .Select(static (method, _) => method!);

        // Projected to an int before it enters the pipeline: the options provider itself is a new
        // instance on every compilation, so combining it directly would invalidate the source output
        // on every keystroke, while the parsed value compares equal and keeps the output cached.
        var maxTestCasesPerMethod = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GeneratorOptions.ReadMaxTestCasesPerMethod(provider.GlobalOptions));

        // Collect() compares the batched arrays element-wise, and the descriptors are value models,
        // so an edit that leaves the discovered tests unchanged leaves this input cached.
        var combined = testMethods.Collect()
            .Combine(beforeMethods.Collect())
            .Combine(afterMethods.Collect())
            .Combine(maxTestCasesPerMethod);

        context.RegisterSourceOutput(combined, static (spc, source) =>
        {
            var (((tests, beforeLifecycle), afterLifecycle), maxTestCases) = source;
            EmitRegistry(spc, tests, beforeLifecycle, afterLifecycle, maxTestCases);
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

    private static TestMethodDescriptor? TransformTestMethod(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        return TransformMethod(
            methodSymbol,
            KnownReturnTypes.Create(context.SemanticModel.Compilation),
            KnownDataSourceTypes.Create(context.SemanticModel.Compilation),
            context.SemanticModel.Compilation.Assembly);
    }

    private static TestMethodDescriptor? TransformMethod(
        IMethodSymbol methodSymbol,
        KnownReturnTypes knownTypes,
        KnownDataSourceTypes knownDataSourceTypes,
        IAssemblySymbol? compilingAssembly)
    {
        var typeSymbol = methodSymbol.ContainingType;
        var fullyQualifiedTypeName = AttributeHelper.GetFullyQualifiedTypeName(typeSymbol);
        var id = AttributeHelper.CreateTestId(methodSymbol);
        var customDisplayName = AttributeHelper.GetCustomDisplayName(methodSymbol);
        var displayName = customDisplayName ?? methodSymbol.Name;
        var displayNameFormatterType = AttributeHelper.GetDisplayNameFormatterType(methodSymbol, typeSymbol, compilingAssembly);
        var (notInParallel, constraintKeys) = AttributeHelper.GetNotInParallelInfo(methodSymbol, typeSymbol);
        var parallelGroup = AttributeHelper.GetParallelGroup(methodSymbol, typeSymbol);
        var parallelLimit = AttributeHelper.GetParallelLimit(methodSymbol, typeSymbol);
        var dependencyMetadata = AttributeHelper.GetDependencyMetadata(methodSymbol);
        var (isSkipped, skipReason) = AttributeHelper.GetSkipInfo(methodSymbol);
        var (isExplicit, explicitReason) = AttributeHelper.GetExplicitInfo(methodSymbol, typeSymbol);
        var argumentSets = DataSourceAttributeReader.GetArgumentSets(methodSymbol);
        var testDataSources = DataSourceAttributeReader.GetTestDataSources(methodSymbol, knownDataSourceTypes);
        var classDataSources = DataSourceAttributeReader.GetClassDataSources(methodSymbol);
        var parameters = AttributeHelper.GetParameters(methodSymbol);
        var categories = AttributeHelper.GetCategories(methodSymbol, typeSymbol);
        var tags = AttributeHelper.GetTags(methodSymbol, typeSymbol);
        var constructorMetadata = AttributeHelper.GetTestClassConstructorMetadata(typeSymbol);
        var timeoutMs = AttributeHelper.GetTimeout(methodSymbol, typeSymbol);
        var (retryCount, retryDelayMs, retryPolicyTypeName, isFlaky, flakyReason) = AttributeHelper.GetRetryInfo(methodSymbol, typeSymbol, compilingAssembly);
        var repeatCount = AttributeHelper.GetRepeatCount(methodSymbol);
        var matrixParameters = DataSourceAttributeReader.GetMatrixParameters(methodSymbol);
        var matrixExclusions = DataSourceAttributeReader.GetMatrixExclusions(methodSymbol);
        var combinedParameterSources = DataSourceAttributeReader.GetCombinedParameterSources(methodSymbol, knownDataSourceTypes);
        var priority = AttributeHelper.GetExecutionPriority(methodSymbol, typeSymbol);
        var (cultureName, uiCultureName) = AttributeHelper.GetCultureNames(methodSymbol, typeSymbol);
        var inheritedLifecycleMethods = LifecycleMethodFactory.CollectInherited(
            typeSymbol, knownTypes, compilingAssembly);
        var unreachableInheritedTypeName = AttributeHelper.GetUnreachableEmittedTypeName(
            methodSymbol, typeSymbol, compilingAssembly);

        return new TestMethodDescriptor(
            id,
            displayName,
            fullyQualifiedTypeName,
            methodSymbol.Name,
            notInParallel,
            constraintKeys,
            parallelGroup,
            parallelLimit,
            dependencyMetadata.Dependencies,
            dependencyMetadata.DependencyInfos,
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
            GetMethodReturnKind(methodSymbol, knownTypes),
            HasTrailingCancellationToken(parameters),
            constructorMetadata.Kind,
            constructorMetadata.RequiresTestOutput,
            constructorMetadata.RequiresTestContext,
            timeoutMs,
            retryCount,
            retryDelayMs,
            retryPolicyTypeName,
            isFlaky,
            flakyReason,
            customDisplayName,
            displayNameFormatterType,
            repeatCount,
            matrixParameters,
            matrixExclusions,
            combinedParameterSources,
            priority,
            cultureName,
            uiCultureName,
            inheritedLifecycleMethods,
            unreachableInheritedTypeName);
    }

    /// <summary>
    /// Builds the descriptor for one lifecycle attribute kind.
    /// </summary>
    /// <remarks>
    /// [Before] and [After] arrive from separate providers, so a method carrying both yields two
    /// descriptors. Every consumer selects methods by scope, and a scope is only ever filled from
    /// one of the two providers, so the emitted arrays keep their declaration order.
    /// </remarks>
    private static LifecycleMethodDescriptor? TransformLifecycleMethod(
        GeneratorAttributeSyntaxContext context,
        bool isBefore)
    {
        if (context.TargetSymbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        return LifecycleMethodFactory.TryCreate(
            methodSymbol,
            AttributeHelper.GetFullyQualifiedTypeName(methodSymbol.ContainingType),
            KnownReturnTypes.Create(context.SemanticModel.Compilation),
            context.SemanticModel.Compilation.Assembly,
            isBefore);
    }

    private static MethodReturnKind GetMethodReturnKind(
        IMethodSymbol methodSymbol,
        KnownReturnTypes knownTypes)
    {
        // The shared classifier reports async void as Void because the analyzers report it
        // through AsyncVoidTestAnalyzer instead. The generator cannot emit an awaitable delegate
        // for it, so it rejects the case here before classifying.
        if (methodSymbol.IsAsync && methodSymbol.ReturnsVoid)
        {
            return MethodReturnKind.Unsupported;
        }

        return knownTypes.Classify(methodSymbol);
    }

    private static bool HasTrailingCancellationToken(EquatableArray<ParameterDescriptor> parameters) =>
        parameters.Length > 0 &&
        parameters[parameters.Length - 1].DisplayTypeName == WellKnownTypeNames.CancellationToken;

    private static void EmitRegistry(
        SourceProductionContext context,
        ImmutableArray<TestMethodDescriptor> tests,
        ImmutableArray<LifecycleMethodDescriptor> beforeLifecycle,
        ImmutableArray<LifecycleMethodDescriptor> afterLifecycle,
        int maxTestCasesPerMethod)
    {
        // Ordinal ordering by test id keeps the emitted registry stable across compilations,
        // which is what lets the snapshot tests compare generated text byte for byte.
        var allTests = tests
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        // Runs against every discovered test, before the expansion filter drops any of them, so a
        // method that is dropped for being oversized still counts as a resolved [DependsOn] target.
        TestMethodValidator.ValidateAll(context, allTests);
        LifecycleMethodValidator.ValidateAll(context, allTests, beforeLifecycle, afterLifecycle);

        var emittableTests = TestCaseExpansionValidator.RemoveOverLimitTests(
            context, allTests, maxTestCasesPerMethod);

        var source = RegistryEmitter.Emit(emittableTests, beforeLifecycle, afterLifecycle);
        context.AddSource("GeneratedTestRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
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

        // The verbatim literal carries whatever newlines this file had when the generator was
        // compiled, which depends on the checkout's line-ending normalization. Normalizing to LF
        // keeps Program.g.cs byte-identical everywhere and matches the registry emitter, so both
        // generated files share one newline convention.
        context.AddSource("Program.g.cs", SourceText.From(NormalizeNewlines(source), Encoding.UTF8));
    }

    private static string NormalizeNewlines(string source) =>
        source.Replace("\r\n", "\n").Replace("\r", "\n");
}
