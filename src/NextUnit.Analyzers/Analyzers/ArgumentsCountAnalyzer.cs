using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects [Arguments] attribute with mismatched parameter count.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentsCountAnalyzer : DiagnosticAnalyzer
{
    private const string ArgumentsAttributeFullName = "NextUnit.ArgumentsAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ArgumentsCountMismatch);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != ArgumentsAttributeFullName)
            {
                continue;
            }

            // Get the constructor arguments (the params object?[] args)
            var constructorArgs = attribute.ConstructorArguments;
            if (constructorArgs.Length == 0)
            {
                continue;
            }

            // The first constructor argument is the params array
            var argsValue = constructorArgs[0];
            int argumentCount;

            if (argsValue.Kind == TypedConstantKind.Array)
            {
                argumentCount = argsValue.Values.Length;
            }
            else
            {
                // Single value not in array form
                argumentCount = 1;
            }

            var parameterCount = method.Parameters.Length;

            if (argumentCount != parameterCount)
            {
                // Report on the attribute location
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? method.Locations[0];

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ArgumentsCountMismatch,
                    location,
                    method.Name,
                    parameterCount,
                    argumentCount));
            }
        }
    }
}
