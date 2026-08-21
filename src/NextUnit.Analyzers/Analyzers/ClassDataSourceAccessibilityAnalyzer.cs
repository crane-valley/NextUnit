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

        // [Test] is where the generator's pipeline starts, so it is also where the emitted
        // typeof(T) starts. A data source attribute on a method without it is ignored -- reported
        // as NU0013 -- and emits nothing to fail on, so reporting it here would break a build that
        // has no generated code to break.
        if (!HasTestAttribute(method))
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
            // Only the attribute the generator selects is emitted. A parameter carrying [Values]
            // as well as [ValuesFrom<T>] takes the inline values and never constructs T, so
            // reporting T would offer a widening that does not put it back in play.
            var selected = ParameterDataSourceSelector.Select(parameter);
            if (selected.Kind != ParameterDataSourceAttributeKind.ValuesFrom ||
                selected.Attribute is not { } attribute)
            {
                continue;
            }

            if (ClassDataSourceAttributeMatcher.GetValuesFromType(attribute) is { } sourceType)
            {
                ReportIfUnreachable(context, method, attribute, sourceType);
            }
        }
    }

    /// <summary>
    /// Reports whether the method carries <c>[Test]</c>, the attribute the generator's pipeline
    /// keys on.
    /// </summary>
    /// <remarks>
    /// Matched by symbol identity rather than by a formatted display string: this runs for every
    /// attribute on every method in the compilation, and <c>ToDisplayString</c> allocates a string
    /// per call. <c>ContainingType</c> is compared for the reason the shared matchers give -- a
    /// nested type reports the namespace of its outermost container, so a user's own
    /// <c>NextUnit.Container.TestAttribute</c> would otherwise pass as the marker.
    /// </remarks>
    private static bool HasTestAttribute(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is
                {
                    Arity: 0,
                    ContainingType: null,
                    ContainingNamespace:
                    {
                        Name: NextUnitAttributeNames.Namespace,
                        ContainingNamespace.IsGlobalNamespace: true
                    }
                } attributeClass &&
                attributeClass.Name == NextUnitAttributeNames.SimpleNames.Test)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportIfUnreachable(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        ITypeSymbol sourceType)
    {
        // The same rule the emission site asks, rather than a second reading of it: a diagnostic
        // that disagreed with what the generator emits would either miss the CS0122 it exists to
        // replace or report a type that compiles. An unresolved type needs no guard of its own
        // here: CanReachType answers TypeKind.Error as reachable and recurses into type arguments,
        // so a type that does not resolve keeps its own compiler error rather than collecting a
        // visibility complaint on top of it. A local error-type test would be a second copy of
        // that decision, and two copies of "is this reachable" drifting apart is exactly what this
        // rule is built to prevent.
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
