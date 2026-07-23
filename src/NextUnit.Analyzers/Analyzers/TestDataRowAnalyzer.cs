using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Validates constant metadata supplied to <c>TestDataRow&lt;T&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestDataRowAnalyzer : DiagnosticAnalyzer
{
    private const string TestDataRowMetadataName = "TestDataRow`1";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.InvalidTestDataRowMetadata);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeObjectCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BaseObjectCreationExpressionSyntax creation ||
            context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol
            {
                IsGenericType: true,
                MetadataName: TestDataRowMetadataName
            } type ||
            type.ContainingNamespace.ToDisplayString() != "NextUnit" ||
            creation.ArgumentList is null)
        {
            return;
        }

        for (var index = 0; index < creation.ArgumentList.Arguments.Count; index++)
        {
            var argument = creation.ArgumentList.Arguments[index];
            var parameterName = argument.NameColon?.Name.Identifier.ValueText ??
                GetPositionalParameterName(index);

            switch (parameterName)
            {
                case "displayName":
                case "skipReason":
                    ReportInvalidConstant(
                        context,
                        argument.Expression,
                        parameterName,
                        allowNull: true);
                    break;

                case "categories":
                case "tags":
                    foreach (var expression in GetCollectionElements(argument.Expression))
                    {
                        ReportInvalidConstant(
                            context,
                            expression,
                            parameterName,
                            allowNull: false);
                    }
                    break;
            }
        }
    }

    private static string? GetPositionalParameterName(int index) => index switch
    {
        1 => "displayName",
        2 => "categories",
        3 => "tags",
        4 => "skipReason",
        _ => null
    };

    private static IEnumerable<ExpressionSyntax> GetCollectionElements(ExpressionSyntax expression)
    {
        return expression switch
        {
            CollectionExpressionSyntax collection => collection.Elements
                .OfType<ExpressionElementSyntax>()
                .Select(element => element.Expression),
            ArrayCreationExpressionSyntax { Initializer: not null } array =>
                array.Initializer.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: not null } array =>
                array.Initializer.Expressions,
            _ => Array.Empty<ExpressionSyntax>()
        };
    }

    private static void ReportInvalidConstant(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        string parameterName,
        bool allowNull)
    {
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        if (!constant.HasValue ||
            (constant.Value is null && allowNull) ||
            (constant.Value is string value && !string.IsNullOrWhiteSpace(value)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidTestDataRowMetadata,
            expression.GetLocation(),
            parameterName));
    }
}
