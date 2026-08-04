using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a malformed culture name on <c>[Culture]</c> or <c>[UICulture]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only names that no machine could accept are reported. Whether a well-formed name matches an
/// installed culture depends on the globalization data of the machine executing the test, not of the
/// machine running the build, so rejecting an unknown-but-well-formed name here would fail builds for
/// cultures that exist perfectly well where the tests run. Those are reported against the test itself
/// when it runs.
/// </para>
/// <para>
/// The rule mirrors what the runtime rejects outright: a name may contain only ASCII letters, digits,
/// <c>'-'</c> and <c>'_'</c>, and may not begin or end with a separator or run two together. The
/// empty string is valid and means the invariant culture.
/// </para>
/// <para>
/// Matching runs on the attribute syntax rather than on symbols so that one registration covers the
/// method, class, and assembly forms alike. A symbol action never visits an assembly-level attribute,
/// and the compilation action that would have to cover it turns the rule into a compilation-end
/// diagnostic, which is a worse trade for the far more common method-level case.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CultureNameAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MalformedCultureName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;

        // The cheap syntactic gate first: this action sees every attribute in the compilation, and
        // resolving a symbol for each one to discard almost all of them is the cost worth avoiding.
        if (!NameCouldMatch(attribute.Name))
        {
            return;
        }

        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol;
        if (symbol?.ContainingType?.ToDisplayString() is not
            (NextUnitAttributeNames.Culture or NextUnitAttributeNames.UICulture))
        {
            return;
        }

        var argument = attribute.ArgumentList.Arguments[0];
        var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not string name || IsWellFormed(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MalformedCultureName,
            argument.GetLocation(),
            name));
    }

    /// <summary>
    /// Whether the written attribute name could be one of the culture attributes, before the
    /// semantic model is consulted.
    /// </summary>
    private static bool NameCouldMatch(NameSyntax nameSyntax)
    {
        var simpleName = nameSyntax switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null
        };

        // Both the written-out and the elided-suffix spellings, since C# allows either.
        return simpleName is "Culture" or "UICulture"
            or NextUnitAttributeNames.SimpleNames.Culture
            or NextUnitAttributeNames.SimpleNames.UICulture;
    }

    private static bool IsWellFormed(string name)
    {
        // The invariant culture.
        if (name.Length == 0)
        {
            return true;
        }

        if (IsSeparator(name[0]) || IsSeparator(name[name.Length - 1]))
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (IsSeparator(c))
            {
                if (IsSeparator(name[i - 1]))
                {
                    return false;
                }

                continue;
            }

            // Deliberately ASCII-only: the runtime accepts no other letters or digits in a culture
            // name, so admitting, say, a full-width digit here would accept a name it rejects.
            var isAsciiLetterOrDigit =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9');

            if (!isAsciiLetterOrDigit)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSeparator(char c) => c is '-' or '_';
}
