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
            DiagnosticDescriptors.TestDataMemberUnsupportedAwaitable);

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

        // Check method-level [TestData] attributes
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == NextUnitAttributeNames.TestData)
            {
                ValidateMemberReference(context, method, attribute, knownDataSourceTypes, validateRowType: true);
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
                    ValidateMemberReference(context, method, attribute, knownDataSourceTypes, validateRowType: false);
                }
            }
        }
    }

    private static void ValidateMemberReference(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute,
        KnownDataSourceTypes knownDataSourceTypes,
        bool validateRowType)
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

        // Look for a static member (property, method, or field)
        // Note: Runtime uses BindingFlags.Public | BindingFlags.NonPublic, so private/protected are valid
        var members = targetType.GetMembers(memberName);
        var validMember = members.FirstOrDefault(member =>
            (member is IPropertySymbol property && property.IsStatic) ||
            (member is IMethodSymbol memberMethod && memberMethod.IsStatic) ||
            (member is IFieldSymbol field && field.IsStatic));

        if (validMember is null)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? method.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TestDataMemberNotFound,
                location,
                memberName,
                targetType.Name));
            return;
        }

        if (validateRowType)
        {
            ValidateRowType(
                context,
                method,
                attribute,
                memberName,
                GetMemberType(validMember),
                knownDataSourceTypes);
        }
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

    /// <summary>
    /// Gets the type a data source member exposes.
    /// </summary>
    /// <remarks>
    /// A method taking a single cancellation token is accepted because an asynchronous source may
    /// observe the discovery token; the generator binds that signature the same way.
    /// </remarks>
    private static ITypeSymbol? GetMemberType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type,
        IFieldSymbol field => field.Type,
        IMethodSymbol method when method.Parameters.Length == 0 => method.ReturnType,
        IMethodSymbol method when method.Parameters.Length == 1 &&
            method.Parameters[0].Type.ToDisplayString() == WellKnownTypeNames.CancellationToken =>
            method.ReturnType,
        _ => null
    };

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

    private static ITypeSymbol UnwrapTestDataRow(ITypeSymbol elementType)
    {
        if (elementType is INamedTypeSymbol
            {
                IsGenericType: true,
                MetadataName: NextUnitAttributeNames.MetadataNames.TestDataRow
            } namedType &&
            namedType.ContainingNamespace.ToDisplayString() == "NextUnit")
        {
            return namedType.TypeArguments[0];
        }

        return elementType;
    }
}
