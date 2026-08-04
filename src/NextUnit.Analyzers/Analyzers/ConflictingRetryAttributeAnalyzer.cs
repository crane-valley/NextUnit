using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a target carrying both <c>[Retry]</c> and <c>[Retry&lt;TPolicy&gt;]</c>.
/// </summary>
/// <remarks>
/// The two attributes are distinct types, so the compiler's <c>AllowMultiple = false</c> check does
/// not catch the combination even though both declare the same attempt budget and delay. The
/// generator resolves it by honoring the policy-bearing attribute, but nothing in the source says
/// which one the author meant, so it is reported rather than silently picked.
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
        AttributeData? plainRetry = null;
        AttributeData? policyRetry = null;

        foreach (var attribute in context.Symbol.GetAttributes())
        {
            if (IsPolicyRetry(attribute))
            {
                policyRetry ??= attribute;
            }
            else if (attribute.AttributeClass?.ToDisplayString() == NextUnitAttributeNames.Retry)
            {
                plainRetry ??= attribute;
            }
        }

        if (plainRetry is null || policyRetry is null)
        {
            return;
        }

        // Reported on the policy-free attribute: it is the one the generator drops, so the location
        // points at the declaration that has no effect.
        var location = plainRetry.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? context.Symbol.Locations[0];

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConflictingRetryAttributes,
            location,
            context.Symbol.Name));
    }

    private static bool IsPolicyRetry(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { IsGenericType: true } attributeClass)
        {
            return false;
        }

        var constructedFrom = attributeClass.ConstructedFrom;
        return constructedFrom.MetadataName == NextUnitAttributeNames.MetadataNames.RetryAttributeGeneric &&
            constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitAttributeNames.Namespace;
    }
}
