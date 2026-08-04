using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a target carrying more than one retry attribute.
/// </summary>
/// <remarks>
/// <c>[Retry]</c> and every constructed <c>[Retry&lt;TPolicy&gt;]</c> are distinct types, so the
/// compiler's <c>AllowMultiple = false</c> check does not catch the combination even though all of
/// them declare the same attempt budget and delay. Two different policies are just as ambiguous as a
/// policy alongside a plain budget. The generator has to resolve it somehow and picks the first
/// policy-bearing declaration, but nothing in the source says which one the author meant, so it is
/// reported rather than silently picked.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConflictingRetryAttributeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ConflictingRetryAttributes);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var seen = false;

        foreach (var attribute in context.Symbol.GetAttributes())
        {
            if (!IsRetry(attribute))
            {
                continue;
            }

            if (!seen)
            {
                // The first declaration is the anchor; every later one is what makes the budget
                // ambiguous, so the diagnostic points at the redundant declaration rather than at
                // the one a reader would naturally treat as the intended setting.
                seen = true;
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? context.Symbol.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ConflictingRetryAttributes,
                location,
                context.Symbol.Name));
        }
    }

    private static bool IsRetry(AttributeData attribute) =>
        RetryAttributeMatcher.IsRetry(attribute);
}
