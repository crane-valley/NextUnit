using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NextUnit.Analyzers.Analyzers;

/// <summary>
/// Analyzer that detects unsupported test and lifecycle method return types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsupportedMethodReturnTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> _supportedAttributeNames =
        ImmutableHashSet.Create(
            "NextUnit.TestAttribute",
            "NextUnit.BeforeAttribute",
            "NextUnit.AfterAttribute");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.UnsupportedMethodReturnType);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var knownTypes = KnownTypes.Create(startContext.Compilation);
            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, knownTypes),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        KnownTypes knownTypes)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!HasSupportedAttribute(method) || IsSupportedReturnType(method, knownTypes))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.UnsupportedMethodReturnType,
            method.Locations[0],
            method.Name,
            method.ReturnType.ToDisplayString()));
    }

    private static bool HasSupportedAttribute(IMethodSymbol method) =>
        method.GetAttributes().Any(
            attribute => _supportedAttributeNames.Contains(attribute.AttributeClass?.ToDisplayString() ?? ""));

    private static bool IsSupportedReturnType(
        IMethodSymbol method,
        KnownTypes knownTypes)
    {
        if (method.ReturnsVoid)
        {
            return true;
        }

        if (method.ReturnType is not INamedTypeSymbol returnType)
        {
            return false;
        }

        if (knownTypes.Task is not null && IsTaskType(returnType, knownTypes.Task))
        {
            return true;
        }

        return (knownTypes.ValueTask is not null &&
                SymbolEqualityComparer.Default.Equals(returnType, knownTypes.ValueTask)) ||
            (knownTypes.GenericValueTask is not null &&
             SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, knownTypes.GenericValueTask));
    }

    private static bool IsTaskType(
        INamedTypeSymbol returnType,
        INamedTypeSymbol taskType)
    {
        for (INamedTypeSymbol? current = returnType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, taskType))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class KnownTypes
    {
        private KnownTypes(
            INamedTypeSymbol? task,
            INamedTypeSymbol? valueTask,
            INamedTypeSymbol? genericValueTask)
        {
            Task = task;
            ValueTask = valueTask;
            GenericValueTask = genericValueTask;
        }

        public INamedTypeSymbol? Task { get; }

        public INamedTypeSymbol? ValueTask { get; }

        public INamedTypeSymbol? GenericValueTask { get; }

        public static KnownTypes Create(Compilation compilation) =>
            new(
                compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
    }
}
