using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects data-source attributes on methods without [Test].
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingTestAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string TestAttributeFullName = "NextUnit.TestAttribute";
    private const string ArgumentsAttributeFullName = "NextUnit.ArgumentsAttribute";
    private const string TestDataAttributeFullName = "NextUnit.TestDataAttribute";
    private const string MatrixAttributeFullName = "NextUnit.MatrixAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DataSourceWithoutTest);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method.GetAttributes().Any(
            attribute => attribute.AttributeClass?.ToDisplayString() == TestAttributeFullName))
        {
            return;
        }

        var dataSourceName = GetDataSourceName(method);
        if (dataSourceName is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DataSourceWithoutTest,
            method.Locations[0],
            method.Name,
            dataSourceName));
    }

    private static string? GetDataSourceName(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            switch (attribute.AttributeClass?.ToDisplayString())
            {
                case ArgumentsAttributeFullName:
                    return "Arguments";
                case TestDataAttributeFullName:
                    return "TestData";
            }
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.GetAttributes().Any(
                attribute => attribute.AttributeClass?.ToDisplayString() == MatrixAttributeFullName))
            {
                return "Matrix";
            }
        }

        return null;
    }
}
