using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects a <c>[ClassDataSource&lt;T&gt;]</c> or <c>[ValuesFrom&lt;T&gt;]</c> source
/// type the generated registry cannot name.
/// </summary>
/// <remarks>
/// Both attributes are emitted as <c>typeof(T)</c> plus a <c>new T()</c> factory inside
/// <c>NextUnit.Generated.GeneratedTestRegistry</c> rather than reflected over, which is what keeps
/// them AOT-safe. The cost is that the type has to be visible from there: a private or protected
/// nested source satisfies the <c>IEnumerable</c> and <c>new()</c> constraints at the attribute and
/// then fails the consumer's build with <c>CS0122</c> in a file the user did not write. Reported
/// here so the error names the type and the fix.
/// <para>
/// The member paths withhold an unreachable type from the registry instead of reporting it this
/// way, and that is deliberately not mirrored: the emitted factory is the only way a class data
/// source is constructed, so withholding the type would trade a build error for a source that
/// silently supplies no rows.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassDataSourceAccessibilityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ClassDataSourceTypeNotAccessible);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // A method symbol action also fires for constructors, accessors, operators, and synthesized
        // methods, none of which the generator reads a data source from. Skipping them early also
        // keeps the fallback location safe: a synthesized symbol can have no location at all.
        if (method.MethodKind != MethodKind.Ordinary || method.Locations.IsEmpty)
        {
            return;
        }

        foreach (var attribute in method.GetAttributes())
        {
            // Every type argument is emitted, so each is reported separately: one unreachable
            // source in [ClassDataSource<A, B>] leaves the other perfectly usable, and naming the
            // attribute rather than the type would not say which half to widen.
            foreach (var sourceType in ClassDataSourceAttributeMatcher.GetClassDataSourceTypes(attribute))
            {
                ReportIfUnreachable(context, method, attribute, sourceType);
            }
        }

        foreach (var parameter in method.Parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (ClassDataSourceAttributeMatcher.GetValuesFromType(attribute) is { } sourceType)
                {
                    ReportIfUnreachable(context, method, attribute, sourceType);
                }
            }
        }
    }

    private static void ReportIfUnreachable(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        ITypeSymbol sourceType)
    {
        // The same rule the emission site asks, rather than a second reading of it: a diagnostic
        // that disagreed with what the generator emits would either miss the CS0122 it exists to
        // replace or report a type that compiles.
        if (GeneratedRegistryAccess.CanReachType(sourceType, context.Compilation.Assembly))
        {
            return;
        }

        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? method.Locations[0];

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ClassDataSourceTypeNotAccessible,
            location,
            sourceType.ToDisplayString()));
    }
}
