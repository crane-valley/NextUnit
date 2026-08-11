using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a <c>[ParallelLimit]</c> value the parallel scheduler cannot use.
/// </summary>
/// <remarks>
/// <para>
/// The declared limit reaches <c>ParallelOptions.MaxDegreeOfParallelism</c>, which accepts a
/// positive degree of parallelism or <c>-1</c> for unlimited and throws for anything else. That
/// throw happens while a batch is being started, so it aborts the whole run rather than failing the
/// test that declared the value, which is why the value is rejected at build time instead.
/// </para>
/// <para>
/// Matching runs on the attribute syntax rather than on symbols so that one registration covers the
/// method, class, and assembly forms alike, following <see cref="CultureNameAnalyzer"/>: a symbol
/// action never visits an assembly-level attribute, and <c>[ParallelLimit]</c> is assembly-targetable
/// and resolved from the assembly by the generator.
/// </para>
/// <para>
/// The attribute is identified by comparing symbols, not by how it is spelled at the use site, so a
/// <c>using Throttle = NextUnit.ParallelLimitAttribute;</c> alias is still checked. The
/// compilation-start lookup keeps that affordable: a compilation that does not reference NextUnit
/// registers no per-attribute work.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParallelLimitValueAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InvalidParallelLimit);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var parallelLimit = compilationStart.Compilation
                .GetTypeByMetadataName(NextUnitAttributeNames.ParallelLimit);

            if (parallelLimit is null)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAttribute(nodeContext, parallelLimit),
                SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, INamedTypeSymbol parallelLimit)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var attributeType = context.SemanticModel
            .GetSymbolInfo(attribute, context.CancellationToken).Symbol?.ContainingType;

        // An attribute that does not bind needs no separate guard here, unlike in
        // CultureNameAnalyzer: this rule watches one type, and the action is registered only once
        // that type resolves, so a null attributeType cannot compare equal to it.
        if (!SymbolEqualityComparer.Default.Equals(attributeType, parallelLimit))
        {
            return;
        }

        var argument = attribute.ArgumentList.Arguments[0];
        var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);

        if (constant.Value is not int limit || IsSupported(limit))
        {
            // A value that is not a compile-time constant cannot be inspected here. The generator
            // reads the same argument and drops what it cannot use, so an unusable one bounds the
            // run by the enclosing declaration rather than throwing.
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidParallelLimit,
            argument.GetLocation(),
            limit));
    }

    /// <summary>
    /// The values <c>ParallelOptions.MaxDegreeOfParallelism</c> accepts.
    /// </summary>
    private static bool IsSupported(int limit) => limit > 0 || limit == -1;
}
