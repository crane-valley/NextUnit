using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NextUnit.CodeAnalysis.Shared;
namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects [TestData] and [ValuesFromMember] attributes
/// referencing non-existent or inaccessible members.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestDataMemberAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.TestDataMemberNotFound,
            DiagnosticDescriptors.TestDataRowTypeMismatch,
            DiagnosticDescriptors.TestDataMemberUnsupportedAwaitable,
            DiagnosticDescriptors.DataSourceMemberNotAccessible,
            DiagnosticDescriptors.DataSourceCancellationTokenOnSyncSource);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var knownDataSourceTypes = KnownDataSourceTypes.Create(startContext.Compilation);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, knownDataSourceTypes),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, KnownDataSourceTypes knownDataSourceTypes)
    {
        var method = (IMethodSymbol)context.Symbol;

        // A method symbol action also fires for constructors, accessors, operators, and synthesized
        // methods, none of which can carry a data source attribute. Skipping them early also keeps
        // the fallback location safe: a synthesized symbol can have no location at all, and
        // Locations[0] on one of those would crash the analyzer rather than report a diagnostic.
        if (method.MethodKind != MethodKind.Ordinary || method.Locations.IsEmpty)
        {
            return;
        }

        // Check method-level [TestData] attributes
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == NextUnitAttributeNames.TestData)
            {
                ValidateMemberReference(context, method, attribute, knownDataSourceTypes, isTestDataSource: true);
            }
            else if (IsClassDataSourceAttribute(attribute))
            {
                ValidateClassDataSourceTypes(context, method, attribute, knownDataSourceTypes);
            }
        }

        // Check parameter-level [ValuesFromMember] attributes
        foreach (var parameter in method.Parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == NextUnitAttributeNames.ValuesFromMember)
                {
                    ValidateMemberReference(context, method, attribute, knownDataSourceTypes, isTestDataSource: false);
                }
            }
        }
    }

    private static void ValidateMemberReference(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        KnownDataSourceTypes knownDataSourceTypes,
        bool isTestDataSource)
    {
        var constructorArgs = attribute.ConstructorArguments;
        if (constructorArgs.Length == 0)
        {
            return;
        }

        // First argument is the member name
        if (constructorArgs[0].Value is not string memberName)
        {
            return;
        }

        // Second argument (optional) is the member type
        INamedTypeSymbol? memberType = null;
        if (constructorArgs.Length > 1 && constructorArgs[1].Value is INamedTypeSymbol specifiedType)
        {
            memberType = specifiedType;
        }

        // Also check named argument "MemberType"
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "MemberType" && namedArg.Value.Value is INamedTypeSymbol namedType)
            {
                memberType = namedType;
            }
        }

        // Default to the containing type if no type specified
        var targetType = memberType ?? method.ContainingType;
        if (targetType is null)
        {
            return;
        }

        // Every answer below comes from one resolution. An independent "does a usable member of
        // this name exist" test was tried and drifted from what the resolver accepts three times,
        // each time leaving a source that binds nothing and reports nothing; the issue taxonomy is
        // now the single truth, and the mapping here covers all of it. Silence is therefore
        // reachable only when a provider is emitted.
        var resolved = DataSourceMemberResolver.Resolve(targetType, memberName, knownDataSourceTypes);

        // A parameter-level source binds only a parameterless member, so a token-taking one is out
        // of its reach whatever else is true of it -- the same expression the shadowing hint uses
        // to decide whether a base type would answer the name.
        var usableHere = resolved.Symbol is not null &&
            (isTestDataSource ||
                resolved.Symbol is IMethodSymbol { Parameters.Length: 0 } or IPropertySymbol or IFieldSymbol);

        if (!usableHere)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? method.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TestDataMemberNotFound,
                location,
                memberName,
                targetType.Name,
                DescribeShadowedBaseType(targetType, memberName, knownDataSourceTypes, isTestDataSource)));
            return;
        }

        if (resolved.Issue == DataSourceBindingIssue.MemberNotAccessible)
        {
            ReportDiagnostic(
                context,
                method,
                attribute,
                DiagnosticDescriptors.DataSourceMemberNotAccessible,
                memberName,
                targetType.Name,
                DescribeShadowedBaseType(targetType, memberName, knownDataSourceTypes, isTestDataSource));
            return;
        }

        if (resolved.Issue == DataSourceBindingIssue.CancellationTokenOnSynchronousSource)
        {
            ReportDiagnostic(
                context,
                method,
                attribute,
                DiagnosticDescriptors.DataSourceCancellationTokenOnSyncSource,
                memberName,
                resolved.MemberType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return;
        }

        // Reported here rather than left to ValidateRowType so that a parameter-level source gets
        // it too: the shape supplies no rows either way, and that path runs for [TestData] alone.
        if (resolved.Issue == DataSourceBindingIssue.UnsupportedAwaitable)
        {
            ReportDiagnostic(
                context,
                method,
                attribute,
                DiagnosticDescriptors.TestDataMemberUnsupportedAwaitable,
                memberName,
                resolved.MemberType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return;
        }

        if (isTestDataSource)
        {
            ValidateRowType(
                context,
                method,
                attribute,
                memberName,
                resolved.MemberType,
                knownDataSourceTypes);
        }
    }

    /// <summary>
    /// Builds the sentence appended to a failed lookup when a base type also declares the name.
    /// </summary>
    /// <remarks>
    /// Inherited lookup stops at the nearest type that declares the name, so a base declaration
    /// further up is not consulted at all. Without this the report describes a member the user can
    /// see plainly declared on a base class as missing or unreachable, with nothing to suggest why.
    /// Empty when no farther type declares the name, which is the ordinary case.
    /// </remarks>
    private static string DescribeShadowedBaseType(
        INamedTypeSymbol targetType,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes,
        bool isTestDataSource)
    {
        var shadowed = DataSourceMemberResolver.FindShadowedDeclaringType(
            targetType,
            memberName,
            knownDataSourceTypes,
            isTestDataSource);

        if (shadowed is null)
        {
            return string.Empty;
        }

        // The prose name is the short one a reader recognises; the typeof operand is fully
        // qualified, because the suggestion is meant to be pasted. A short name would not compile
        // for a base type in another namespace, nested in another type, or generic.
        var readableName = shadowed.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var typeOfOperand = shadowed.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return $". Type '{readableName}' also declares '{memberName}', but only the nearest type " +
            $"declaring that name is used; set MemberType = typeof({typeOfOperand}) to bind it directly";
    }

    private static bool IsClassDataSourceAttribute(AttributeData attribute)
    {
        var attributeClass = attribute.AttributeClass;
        return attributeClass is { IsGenericType: true } &&
            attributeClass.ConstructedFrom.MetadataName.StartsWith(
                NextUnitAttributeNames.MetadataNames.ClassDataSourceAttributePrefix,
                StringComparison.Ordinal) &&
            attributeClass.ContainingNamespace.ToDisplayString() == "NextUnit";
    }

    private static void ValidateClassDataSourceTypes(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        foreach (var sourceType in attribute.AttributeClass!.TypeArguments.OfType<INamedTypeSymbol>())
        {
            ValidateRowType(
                context,
                method,
                attribute,
                sourceType.Name,
                sourceType,
                knownDataSourceTypes);
        }
    }

    private static void ValidateRowType(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        string sourceName,
        ITypeSymbol? sourceType,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var classification = knownDataSourceTypes.Classify(sourceType);

        if (classification.Shape == DataSourceShape.UnsupportedAwaitable)
        {
            ReportDiagnostic(
                context,
                method,
                attribute,
                DiagnosticDescriptors.TestDataMemberUnsupportedAwaitable,
                sourceName,
                sourceType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return;
        }

        var elementType = classification.RowType;
        if (elementType is null)
        {
            return;
        }

        var rowType = UnwrapTestDataRow(elementType);
        if (rowType.SpecialType == SpecialType.System_Object || rowType is IArrayTypeSymbol)
        {
            return;
        }

        var suppliedTypes = rowType is INamedTypeSymbol { IsTupleType: true } tuple
            ? tuple.TupleElements.Select(element => element.Type).ToImmutableArray()
            : ImmutableArray.Create(rowType);
        var targetParameters = method.Parameters
            .Where((parameter, index) =>
                index != method.Parameters.Length - 1 ||
                parameter.Type.ToDisplayString() != WellKnownTypeNames.CancellationToken)
            .ToImmutableArray();

        var isCompatible = suppliedTypes.Length == targetParameters.Length;
        if (isCompatible)
        {
            var compilation = (CSharpCompilation)context.Compilation;
            for (var i = 0; i < suppliedTypes.Length; i++)
            {
                var conversion = compilation.ClassifyConversion(
                    suppliedTypes[i],
                    targetParameters[i].Type);
                if (!conversion.IsImplicit ||
                    conversion.IsUserDefined ||
                    conversion.IsTupleConversion)
                {
                    isCompatible = false;
                    break;
                }
            }
        }

        if (isCompatible)
        {
            return;
        }

        ReportDiagnostic(
            context,
            method,
            attribute,
            DiagnosticDescriptors.TestDataRowTypeMismatch,
            sourceName,
            rowType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            method.Name);
    }

    private static void ReportDiagnostic(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        DiagnosticDescriptor descriptor,
        params object[] messageArguments)
    {
        var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? method.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArguments));
    }

    private static ITypeSymbol UnwrapTestDataRow(ITypeSymbol elementType) =>
        KnownDataSourceTypes.IsTestDataRow(elementType)
            ? ((INamedTypeSymbol)elementType).TypeArguments[0]
            : elementType;
}
