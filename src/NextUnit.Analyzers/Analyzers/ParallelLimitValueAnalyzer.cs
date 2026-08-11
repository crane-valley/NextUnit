using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a non-positive <c>[ParallelLimit]</c> value.
/// </summary>
/// <remarks>
/// <para>
/// The declared limit reaches <c>ParallelOptions.MaxDegreeOfParallelism</c>, whose setter throws for
/// 0 and for anything below -1. That throw happens while a batch is being started, so it aborts the
/// whole run rather than failing the test that declared the value, which is why the value is
/// rejected at build time instead.
/// </para>
/// <para>
/// -1 is rejected as well, even though the setter accepts it. <c>Parallel.ForEachAsync</c> maps a
/// negative degree of parallelism to the processor count rather than to no limit, which is what the
/// attribute already means when it is absent, so -1 declares nothing while still winning the
/// <c>Min</c> that <c>ParallelScheduler</c> takes across a parallel group - it would raise the
/// group's ceiling above a sibling's explicit limit. Rejecting it keeps a declared limit a limit.
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
        var limit = AsInt32(constant.Value);

        if (limit is null or > 0)
        {
            // A value that is not a compile-time constant cannot be inspected here. The generator
            // reads the same argument and drops what it cannot use, so an unusable one bounds the
            // run by the enclosing declaration rather than throwing.
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidParallelLimit,
            argument.GetLocation(),
            limit.Value));
    }

    /// <summary>
    /// Reads the constant as the <c>int</c> the parameter takes.
    /// </summary>
    /// <remarks>
    /// The constant carries the type it was written with, not the parameter's, so
    /// <c>[ParallelLimit((short)0)]</c> arrives here as an <c>Int16</c>. Every type below is
    /// implicitly convertible to <c>int</c> and so can reach this parameter; the wider integral and
    /// the floating-point types cannot, and are left to the compiler to reject. Missing one of these
    /// would let a value through that the generator, which reads the already-converted
    /// <c>AttributeData</c> argument, still drops - silently, which is what this rule exists to
    /// prevent.
    /// </remarks>
    private static int? AsInt32(object? value) => value switch
    {
        int number => number,
        sbyte number => number,
        byte number => number,
        short number => number,
        ushort number => number,
        char number => number,
        _ => null,
    };
}
