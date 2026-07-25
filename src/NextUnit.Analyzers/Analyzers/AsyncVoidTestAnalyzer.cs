using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects async void test and lifecycle methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidTestAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.AsyncVoidTest);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!HasSupportedAttribute(method))
        {
            return;
        }

        if (method.IsAsync && method.ReturnsVoid)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.AsyncVoidTest,
                method.Locations[0],
                method.Name));
        }
    }

    private static bool HasSupportedAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(
            attribute => NextUnitAttributeNames.TestAndLifecycle.Contains(attribute.AttributeClass?.ToDisplayString() ?? ""));
}
