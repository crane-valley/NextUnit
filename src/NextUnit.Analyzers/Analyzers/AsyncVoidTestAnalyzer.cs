using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects async void test and lifecycle methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidTestAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> _supportedAttributeNames =
        ImmutableHashSet.Create(
            "NextUnit.TestAttribute",
            "NextUnit.BeforeAttribute",
            "NextUnit.AfterAttribute");

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
            attribute => _supportedAttributeNames.Contains(attribute.AttributeClass?.ToDisplayString() ?? ""));
}
