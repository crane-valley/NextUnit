using System.Collections.Concurrent;
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
        context.RegisterCompilationStartAction(static startContext =>
        {
            var testMethodsByType =
                new ConcurrentDictionary<INamedTypeSymbol, ImmutableHashSet<string>>(
                    SymbolEqualityComparer.Default);

            startContext.RegisterSymbolAction(methodContext =>
            {
                var method = (IMethodSymbol)methodContext.Symbol;
                var containingType = method.ContainingType;
                if (containingType is null)
                {
                    return;
                }

                AnalyzeMethod(methodContext, containingType, testMethodsByType);
            }, SymbolKind.Method);
        });
    }

    private static ImmutableHashSet<string> GetTestMethodNames(INamedTypeSymbol containingType)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var member in containingType.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                foreach (var attribute in method.GetAttributes())
                {
                    if (attribute.AttributeClass?.ToDisplayString() == TestAttributeFullName)
                    {
                        builder.Add(method.Name);
                        break;
                    }
                }
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol containingType,
        ConcurrentDictionary<INamedTypeSymbol, ImmutableHashSet<string>> testMethodsByType)
    {
        var method = (IMethodSymbol)context.Symbol;
        ImmutableHashSet<string>? testMethodNames = null;

        // Check each [DependsOn] attribute
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != DependsOnAttributeFullName)
            {
                continue;
            }

            testMethodNames ??= testMethodsByType.GetOrAdd(
                containingType,
                static type => GetTestMethodNames(type));

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
                // For qualified names (with dot), extract and validate
                var dotIndex = dependencyName.LastIndexOf('.');
                string methodName;

                if (dotIndex >= 0)
                {
                    var classPart = dependencyName.Substring(0, dotIndex);
                    methodName = dependencyName.Substring(dotIndex + 1);

                    // Skip validation if referencing a different class
                    if (!string.Equals(classPart, containingType.Name, StringComparison.Ordinal) &&
                        !classPart.EndsWith("." + containingType.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }
                else
                {
                    methodName = dependencyName;
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
