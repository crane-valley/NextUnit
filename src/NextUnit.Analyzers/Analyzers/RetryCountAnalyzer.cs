using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a retry count below 1 on <c>[Retry]</c> or <c>[Retry&lt;TPolicy&gt;]</c>.
/// </summary>
/// <remarks>
/// Both attributes reject the value in their constructor, but that guard never runs: the generator
/// reads the attribute arguments from the symbol and emits the count into the descriptor without ever
/// constructing the attribute. A count of 0 then leaves the engine's retry loop with nothing to
/// execute and aborts the run with an internal error, so the value is rejected at build time, exactly
/// as <c>NU0006</c> rejects a non-positive timeout.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RetryCountAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InvalidRetryCount);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        foreach (var attribute in context.Symbol.GetAttributes())
        {
            if (!IsRetry(attribute) || attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not int count || count >= 1)
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? context.Symbol.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidRetryCount,
                location,
                count));
        }
    }

    private static bool IsRetry(AttributeData attribute) =>
        RetryAttributeMatcher.IsRetry(attribute);
}
