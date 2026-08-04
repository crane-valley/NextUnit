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
/// <para>
/// The attribute is identified by comparing symbols, not by how it is spelled at the use site: a
/// <c>using Locale = NextUnit.CultureAttribute;</c> alias is still the same attribute and still has
/// to be checked. The compilation-start lookup is what keeps that affordable - a compilation that
/// does not reference NextUnit at all registers no per-attribute work.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CultureNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Stands in for a null culture name in the diagnostic message, where the empty string would
    /// read as the invariant culture - which is valid, and the opposite of what is being reported.
    /// </summary>
    private const string NullNameDisplay = "<null>";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MalformedCultureName);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationStart =>
        {
            var culture = compilationStart.Compilation.GetTypeByMetadataName(NextUnitAttributeNames.Culture);
            var uiCulture = compilationStart.Compilation.GetTypeByMetadataName(NextUnitAttributeNames.UICulture);

            if (culture is null && uiCulture is null)
            {
                return;
            }

            compilationStart.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeAttribute(nodeContext, culture, uiCulture),
                SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? culture,
        INamedTypeSymbol? uiCulture)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var attributeType = context.SemanticModel
            .GetSymbolInfo(attribute, context.CancellationToken).Symbol?.ContainingType;

        if (!SymbolEqualityComparer.Default.Equals(attributeType, culture) &&
            !SymbolEqualityComparer.Default.Equals(attributeType, uiCulture))
        {
            return;
        }

        var argument = attribute.ArgumentList.Arguments[0];
        var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
        if (!constant.HasValue)
        {
            // Not a compile-time constant, so there is nothing to inspect here. The runtime still
            // reports an unusable value against the test that declared it.
            return;
        }

        // A null literal reaches here whenever the nullable warning is off or suppressed. It has to
        // be reported rather than ignored: the generated path never constructs the attribute, so the
        // constructor's ArgumentNullException never runs, and treating null as "nothing declared"
        // would silently fall through to the class or assembly culture instead.
        if (constant.Value is null)
        {
            Report(context, argument, NullNameDisplay);
            return;
        }

        if (constant.Value is string name && !IsWellFormed(name))
        {
            Report(context, argument, name);
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax argument, string name) =>
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MalformedCultureName,
            argument.GetLocation(),
            name));

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
