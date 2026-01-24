using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects [TestData] and [ValuesFromMember] attributes
/// referencing non-existent or inaccessible members.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestDataMemberAnalyzer : DiagnosticAnalyzer
{
    private const string TestDataAttributeFullName = "NextUnit.TestDataAttribute";
    private const string ValuesFromMemberAttributeFullName = "NextUnit.ValuesFromMemberAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.TestDataMemberNotFound);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Check method-level [TestData] attributes
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == TestDataAttributeFullName)
            {
                ValidateMemberReference(context, method, attribute);
            }
        }

        // Check parameter-level [ValuesFromMember] attributes
        foreach (var parameter in method.Parameters)
        {
            foreach (var attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == ValuesFromMemberAttributeFullName)
                {
                    ValidateMemberReference(context, method, attribute);
                }
            }
        }
    }

    private static void ValidateMemberReference(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        AttributeData attribute)
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

        // Look for the member
        var members = targetType.GetMembers(memberName);
        var foundAccessibleMember = false;

        foreach (var member in members)
        {
            // Check if member is accessible (public or internal in same assembly)
            if (member.DeclaredAccessibility == Accessibility.Public ||
                member.DeclaredAccessibility == Accessibility.Internal ||
                member.DeclaredAccessibility == Accessibility.ProtectedOrInternal)
            {
                // For properties and methods, they should be static for data sources
                if (member is IPropertySymbol property && property.IsStatic)
                {
                    foundAccessibleMember = true;
                    break;
                }
                else if (member is IMethodSymbol memberMethod && memberMethod.IsStatic)
                {
                    foundAccessibleMember = true;
                    break;
                }
                else if (member is IFieldSymbol field && field.IsStatic)
                {
                    foundAccessibleMember = true;
                    break;
                }
            }
        }

        if (!foundAccessibleMember)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? method.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TestDataMemberNotFound,
                location,
                memberName,
                targetType.Name));
        }
    }
}
