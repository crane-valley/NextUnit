using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Task-like return types resolved once per compilation, plus the return-type classification
/// shared by the source generator and the analyzers.
/// </summary>
/// <remarks>
/// Resolve this once per compilation rather than per symbol: <c>GetTypeByMetadataName</c> is a
/// lookup across every referenced assembly.
/// </remarks>
internal readonly struct KnownReturnTypes
{
    private KnownReturnTypes(
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

    public static KnownReturnTypes Create(Compilation compilation) =>
        new(
            compilation.GetTypeByMetadataName(WellKnownTypeNames.Task),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.ValueTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.GenericValueTask));

    /// <summary>
    /// Classifies the method's return type.
    /// </summary>
    /// <remarks>
    /// <c>async void</c> is deliberately reported as <see cref="MethodReturnKind.Void"/> here.
    /// The generator rejects it before calling this method because it cannot emit an awaitable
    /// delegate for it, while the analyzers leave that case to <c>AsyncVoidTestAnalyzer</c> so a
    /// single method does not report two diagnostics.
    /// </remarks>
    public MethodReturnKind Classify(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
        {
            return MethodReturnKind.Void;
        }

        if (method.ReturnType is not INamedTypeSymbol returnType)
        {
            return MethodReturnKind.Unsupported;
        }

        if (Task is not null && IsTaskType(returnType, Task))
        {
            return MethodReturnKind.Task;
        }

        if ((ValueTask is not null &&
             SymbolEqualityComparer.Default.Equals(returnType, ValueTask)) ||
            (GenericValueTask is not null &&
             SymbolEqualityComparer.Default.Equals(returnType.OriginalDefinition, GenericValueTask)))
        {
            return MethodReturnKind.ValueTask;
        }

        return MethodReturnKind.Unsupported;
    }

    // Task<T> derives from Task, so walk the base chain instead of comparing the original
    // definition: that also accepts user types deriving from Task.
    private static bool IsTaskType(INamedTypeSymbol returnType, INamedTypeSymbol taskType)
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
}
