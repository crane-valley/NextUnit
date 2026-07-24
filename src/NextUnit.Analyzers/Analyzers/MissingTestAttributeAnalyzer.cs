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
    private const string NextUnitNamespaceName = "NextUnit";
    private const string TestAttributeName = "TestAttribute";
    private const string ArgumentsAttributeName = "ArgumentsAttribute";
    private const string TestDataAttributeName = "TestDataAttribute";
    private const string ClassDataSourceAttributeName = "ClassDataSourceAttribute";
    private const string MatrixAttributeName = "MatrixAttribute";
    private const string ValuesAttributeName = "ValuesAttribute";
    private const string ValuesFromMemberAttributeName = "ValuesFromMemberAttribute";
    private const string ValuesFromAttributeName = "ValuesFromAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DataSourceWithoutTest);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    // This runs on every method in the compilation, so it avoids ToDisplayString()
    // (which allocates a formatted string per attribute) and LINQ (which allocates
    // enumerators/closures per call) in favor of plain loops and cheap Name/namespace
    // symbol comparisons.
    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Constructors, property/event accessors, operators, and other non-ordinary
        // method kinds can never carry [Test] and would only produce false positives
        // (e.g. a primary-constructor parameter carrying [Matrix]). Compiler-synthesized
        // members (record equality members, etc.) can also report an empty Locations
        // array, which would throw on the Locations[0] access below.
        if (method.MethodKind != MethodKind.Ordinary || method.Locations.IsEmpty)
        {
            return;
        }

        var methodAttributes = method.GetAttributes();
        var parameters = method.Parameters;

        // The overwhelming majority of methods carry no relevant attributes at all;
        // bail out before touching parameter attribute lists or doing any name matching.
        if (methodAttributes.Length == 0 && !AnyParameterHasAttributes(parameters))
        {
            return;
        }

        if (HasTestAttribute(methodAttributes))
        {
            return;
        }

        var dataSourceName = GetDataSourceName(methodAttributes, parameters);
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

    private static bool AnyParameterHasAttributes(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.GetAttributes().Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTestAttribute(ImmutableArray<AttributeData> methodAttributes)
    {
        foreach (var attribute in methodAttributes)
        {
            if (IsNextUnitAttribute(attribute.AttributeClass, TestAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the name of the first data-source attribute covering all attributes the
    /// generator honors when discovering test cases: method-level [Arguments],
    /// [TestData], [ClassDataSource&lt;T&gt;] and parameter-level [Matrix], [Values],
    /// [ValuesFromMember], [ValuesFrom&lt;T&gt;].
    /// </summary>
    private static string? GetDataSourceName(
        ImmutableArray<AttributeData> methodAttributes,
        ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var attribute in methodAttributes)
        {
            var name = GetMethodLevelDataSourceName(attribute.AttributeClass);
            if (name is not null)
            {
                return name;
            }
        }

        foreach (var parameter in parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                var name = GetParameterLevelDataSourceName(attribute.AttributeClass);
                if (name is not null)
                {
                    return name;
                }
            }
        }

        return null;
    }

    private static string? GetMethodLevelDataSourceName(INamedTypeSymbol? attributeClass)
    {
        if (!IsNextUnitAttribute(attributeClass))
        {
            return null;
        }

        return attributeClass!.Name switch
        {
            ArgumentsAttributeName => "Arguments",
            TestDataAttributeName => "TestData",
            ClassDataSourceAttributeName when attributeClass.IsGenericType => "ClassDataSource",
            _ => null
        };
    }

    private static string? GetParameterLevelDataSourceName(INamedTypeSymbol? attributeClass)
    {
        if (!IsNextUnitAttribute(attributeClass))
        {
            return null;
        }

        return attributeClass!.Name switch
        {
            MatrixAttributeName => "Matrix",
            ValuesAttributeName => "Values",
            ValuesFromMemberAttributeName => "ValuesFromMember",
            ValuesFromAttributeName when attributeClass.IsGenericType => "ValuesFrom",
            _ => null
        };
    }

    /// <summary>
    /// Checks an attribute's class against the NextUnit namespace (and, optionally, an
    /// exact simple name) by walking symbols directly instead of formatting a display
    /// string, since a named type symbol's simple Name already excludes generic arity
    /// (e.g. "ClassDataSourceAttribute" for every ClassDataSourceAttribute&lt;...&gt; arity).
    /// </summary>
    private static bool IsNextUnitAttribute(INamedTypeSymbol? attributeClass, string? expectedName = null)
    {
        if (attributeClass is null)
        {
            return false;
        }

        if (expectedName is not null && attributeClass.Name != expectedName)
        {
            return false;
        }

        var containingNamespace = attributeClass.ContainingNamespace;
        return containingNamespace is { Name: NextUnitNamespaceName } &&
            containingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
    }
}
