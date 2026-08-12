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

        // Look for a static member (property, method, or field) the framework could use at all.
        // Accessibility is not part of this test: the runtime reflection fallback uses
        // BindingFlags.NonPublic, so an unreachable member is a different failure from a missing one
        // and is reported as NU0020 below. Arity is part of it: neither the generated call nor the
        // reflection fallback supplies a type argument, so a generic overload is no more usable than
        // an instance member, which this test has always rejected the same way. The base chain is
        // part of it too, through the same helper the resolver uses, so that a member the resolver
        // now binds is never reported as missing here first.
        var members = DataSourceMemberResolver.GetCandidateMembers(targetType, memberName);
        // The shape test matches what the resolver can actually bind. A method that requires
        // arguments -- including one whose parameters a derived overload declares as optional --
        // is not usable however it is declared, and accepting it here left the source binding
        // nothing while no diagnostic said so.
        var validMember = members.FirstOrDefault(static member =>
            member.IsStatic && DataSourceMemberResolver.HasBindableShape(member));

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

        // Resolve the member the generator will actually bind, not simply the first static member
        // with this name. A type carrying both Rows() and an unsupported Rows(CancellationToken)
        // overload would otherwise be reported for an overload that is never emitted, failing a
        // build that compiles and runs correctly.
        var resolved = DataSourceMemberResolver.Resolve(targetType, memberName, knownDataSourceTypes);

        // A parameter-level source binds only a parameterless member, so a token-taking overload is
        // out of its reach whatever its accessibility. Reporting NU0020 there would name a fix --
        // widen the member -- that does not make it bind.
        var boundHere = isTestDataSource ||
            resolved.Symbol is IMethodSymbol { Parameters.Length: 0 } or IPropertySymbol or IFieldSymbol;

        if (boundHere && resolved.Issue == DataSourceBindingIssue.MemberNotAccessible)
        {
            ReportDiagnostic(
                context,
                method,
                attribute,
                DiagnosticDescriptors.DataSourceMemberNotAccessible,
                memberName,
                targetType.Name);
            return;
        }

        // [ValuesFromMember] expands synchronous collections only, so a token-taking member is out
        // of its reach whatever it returns, and the fix NU0021 names -- return only
        // IAsyncEnumerable<T> -- would not make it bind. That shape stays silent there, as before.
        if (isTestDataSource && resolved.Issue == DataSourceBindingIssue.CancellationTokenOnSynchronousSource)
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
