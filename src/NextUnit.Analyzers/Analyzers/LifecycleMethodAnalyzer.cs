using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that warns about lifecycle methods (Before/After) that may throw exceptions.
/// This is an Info-level diagnostic since exception handling in lifecycle methods is advisory.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LifecycleMethodAnalyzer : DiagnosticAnalyzer
{
    private const string BeforeAttributeFullName = "NextUnit.BeforeAttribute";
    private const string AfterAttributeFullName = "NextUnit.AfterAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.LifecycleMethodThrows);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodSyntax = (MethodDeclarationSyntax)context.Node;

        // Check if method has [Before] or [After] attribute
        var hasLifecycleAttribute = false;
        foreach (var attributeList in methodSyntax.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol attributeConstructor)
                {
                    var attributeTypeName = attributeConstructor.ContainingType.ToDisplayString();
                    if (attributeTypeName == BeforeAttributeFullName ||
                        attributeTypeName == AfterAttributeFullName)
                    {
                        hasLifecycleAttribute = true;
                        break;
                    }
                }
            }

            if (hasLifecycleAttribute)
            {
                break;
            }
        }

        if (!hasLifecycleAttribute)
        {
            return;
        }

        // Check for throw statements that are not inside a try-catch
        if (methodSyntax.Body is null && methodSyntax.ExpressionBody is null)
        {
            return;
        }

        var throwFinder = new ThrowStatementFinder();

        if (methodSyntax.Body is not null)
        {
            throwFinder.Visit(methodSyntax.Body);
        }
        else if (methodSyntax.ExpressionBody is not null)
        {
            throwFinder.Visit(methodSyntax.ExpressionBody);
        }

        if (throwFinder.HasUnhandledThrow)
        {
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax, context.CancellationToken);
            if (methodSymbol is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.LifecycleMethodThrows,
                    methodSyntax.Identifier.GetLocation(),
                    methodSymbol.Name));
            }
        }
    }

    /// <summary>
    /// Visits syntax nodes to find throw statements that are not wrapped in try-catch.
    /// </summary>
    private sealed class ThrowStatementFinder : CSharpSyntaxWalker
    {
        private int _tryDepth;

        public bool HasUnhandledThrow { get; private set; }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            // Don't report throws inside try blocks that have catch clauses
            if (node.Catches.Count > 0)
            {
                _tryDepth++;
                Visit(node.Block);
                _tryDepth--;

                foreach (var catchClause in node.Catches)
                {
                    Visit(catchClause);
                }

                if (node.Finally is not null)
                {
                    Visit(node.Finally);
                }
            }
            else
            {
                base.VisitTryStatement(node);
            }
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            if (_tryDepth == 0)
            {
                HasUnhandledThrow = true;
            }

            base.VisitThrowStatement(node);
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            if (_tryDepth == 0)
            {
                HasUnhandledThrow = true;
            }

            base.VisitThrowExpression(node);
        }
    }
}
