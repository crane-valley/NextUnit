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
    private const string ValuesAttributeFullName = "NextUnit.ValuesAttribute";
    private const string ValuesFromMemberAttributeFullName = "NextUnit.ValuesFromMemberAttribute";
    private const string ClassDataSourceAttributePrefix = "ClassDataSourceAttribute`";
    private const string ValuesFromAttributePrefix = "ValuesFromAttribute`";
    private const string NextUnitNamespace = "NextUnit";

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

        // Constructors, property/event accessors, operators, and other non-ordinary
        // method kinds can never carry [Test] and would only produce false positives
        // (e.g. a primary-constructor parameter carrying [Matrix]). Compiler-synthesized
        // members (record equality members, etc.) can also report an empty Locations
        // array, which would throw on the Locations[0] access below.
        if (method.MethodKind != MethodKind.Ordinary || method.Locations.IsEmpty)
        {
            return;
        }

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

    /// <summary>
    /// Finds the name of the first data-source attribute covering all attributes the
    /// generator honors when discovering test cases: method-level [Arguments],
    /// [TestData], [ClassDataSource&lt;T&gt;] and parameter-level [Matrix], [Values],
    /// [ValuesFromMember], [ValuesFrom&lt;T&gt;].
    /// </summary>
    private static string? GetDataSourceName(IMethodSymbol method)
    {
        var methodLevelName = method.GetAttributes()
            .Select(attribute => GetMethodLevelDataSourceName(attribute.AttributeClass))
            .Where(name => name is not null)
            .FirstOrDefault();

        if (methodLevelName is not null)
        {
            return methodLevelName;
        }

        return method.Parameters
            .SelectMany(parameter => parameter.GetAttributes())
            .Select(attribute => GetParameterLevelDataSourceName(attribute.AttributeClass))
            .Where(name => name is not null)
            .FirstOrDefault();
    }

    private static string? GetMethodLevelDataSourceName(INamedTypeSymbol? attributeClass)
    {
        switch (attributeClass?.ToDisplayString())
        {
            case ArgumentsAttributeFullName:
                return "Arguments";
            case TestDataAttributeFullName:
                return "TestData";
        }

        return IsGenericAttribute(attributeClass, ClassDataSourceAttributePrefix)
            ? "ClassDataSource"
            : null;
    }

    private static string? GetParameterLevelDataSourceName(INamedTypeSymbol? attributeClass)
    {
        switch (attributeClass?.ToDisplayString())
        {
            case MatrixAttributeFullName:
                return "Matrix";
            case ValuesAttributeFullName:
                return "Values";
            case ValuesFromMemberAttributeFullName:
                return "ValuesFromMember";
        }

        return IsGenericAttribute(attributeClass, ValuesFromAttributePrefix)
            ? "ValuesFrom"
            : null;
    }

    private static bool IsGenericAttribute(INamedTypeSymbol? attributeClass, string metadataNamePrefix)
    {
        if (attributeClass is not { IsGenericType: true })
        {
            return false;
        }

        var constructedFrom = attributeClass.ConstructedFrom;
        return constructedFrom.MetadataName.StartsWith(metadataNamePrefix, StringComparison.Ordinal) &&
            constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitNamespace;
    }
}
