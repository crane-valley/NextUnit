using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects async void test methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidTestAnalyzer : DiagnosticAnalyzer
{
    private const string TestAttributeFullName = "NextUnit.TestAttribute";

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

        if (!HasTestAttribute(method))
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

    private static bool HasTestAttribute(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == TestAttributeFullName)
            {
                return true;
            }
        }
        return false;
    }
}
