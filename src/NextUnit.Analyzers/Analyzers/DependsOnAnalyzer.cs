using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects [DependsOn] attributes referencing non-existent test methods.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DependsOnAnalyzer : DiagnosticAnalyzer
{
    private const string DependsOnAttributeFullName = "NextUnit.DependsOnAttribute";
    private const string TestAttributeFullName = "NextUnit.TestAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DependsOnNotFound);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        var containingType = method.ContainingType;

        if (containingType is null)
        {
            return;
        }

        // Get all test method names in the class
        var testMethodNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in containingType.GetMembers())
        {
            if (member is IMethodSymbol memberMethod)
            {
                // Check if method has [Test] attribute
                foreach (var attr in memberMethod.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == TestAttributeFullName)
                    {
                        testMethodNames.Add(memberMethod.Name);
                        break;
                    }
                }
            }
        }

        // Check each [DependsOn] attribute
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != DependsOnAttributeFullName)
            {
                continue;
            }

            // Get the constructor arguments (the params string[] dependencies)
            var constructorArgs = attribute.ConstructorArguments;
            if (constructorArgs.Length == 0)
            {
                continue;
            }

            var dependenciesArg = constructorArgs[0];
            ImmutableArray<TypedConstant> dependencies;

            if (dependenciesArg.Kind == TypedConstantKind.Array)
            {
                dependencies = dependenciesArg.Values;
            }
            else if (dependenciesArg.Kind == TypedConstantKind.Primitive && dependenciesArg.Value is string)
            {
                // Single string argument (not params)
                dependencies = ImmutableArray.Create(dependenciesArg);
            }
            else
            {
                continue;
            }

            foreach (var dependency in dependencies)
            {
                if (dependency.Value is not string dependencyName)
                {
                    continue;
                }

                // Check if the dependency exists
                // For simple names (no dot), look in the same class
                // For qualified names (with dot), we currently only check same class part
                var methodName = dependencyName.Contains('.')
                    ? dependencyName.Substring(dependencyName.LastIndexOf('.') + 1)
                    : dependencyName;

                // If it contains a dot, the class part should match
                if (dependencyName.Contains('.'))
                {
                    var classPart = dependencyName.Substring(0, dependencyName.LastIndexOf('.'));
                    if (classPart != containingType.Name && !classPart.EndsWith("." + containingType.Name))
                    {
                        // References a different class - we skip validation for now
                        // as it would require cross-class analysis
                        continue;
                    }
                }

                if (!testMethodNames.Contains(methodName))
                {
                    var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                        ?? method.Locations[0];

                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.DependsOnNotFound,
                        location,
                        dependencyName,
                        containingType.Name));
                }
            }
        }
    }
}
