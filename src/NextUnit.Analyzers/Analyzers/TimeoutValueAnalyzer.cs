using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects invalid (non-positive) timeout values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TimeoutValueAnalyzer : DiagnosticAnalyzer
{
    private const string TimeoutAttributeFullName = "NextUnit.TimeoutAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InvalidTimeout);

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
            if (attribute.AttributeClass?.ToDisplayString() != TimeoutAttributeFullName)
            {
                continue;
            }

            var constructorArgs = attribute.ConstructorArguments;
            if (constructorArgs.Length == 0)
            {
                continue;
            }

            var millisecondsArg = constructorArgs[0];
            if (millisecondsArg.Value is int milliseconds && milliseconds <= 0)
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? context.Symbol.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidTimeout,
                    location,
                    milliseconds));
            }
        }
    }
}
