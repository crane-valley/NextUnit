using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// The result of classifying a data source member's type.
/// </summary>
internal readonly struct DataSourceClassification
{
    public DataSourceClassification(DataSourceShape shape, ITypeSymbol? rowType)
    {
        Shape = shape;
        RowType = rowType;
    }

    public DataSourceShape Shape { get; }

    /// <summary>
    /// Gets the statically known row type, or <c>null</c> when the member's element type cannot be
    /// determined (a non-generic collection, or a type that is not a data source at all).
    /// </summary>
    public ITypeSymbol? RowType { get; }

    public bool IsAsync =>
        Shape == DataSourceShape.AsyncEnumerable ||
        Shape == DataSourceShape.TaskOfCollection ||
        Shape == DataSourceShape.ValueTaskOfCollection;
}

/// <summary>
/// Data source member types resolved once per compilation, plus the shape classification shared by
/// the source generator and the analyzers.
/// </summary>
/// <remarks>
/// Resolve this once per compilation rather than per symbol: <c>GetTypeByMetadataName</c> is a
/// lookup across every referenced assembly.
/// </remarks>
internal readonly struct KnownDataSourceTypes
{
    private readonly INamedTypeSymbol? _task;
    private readonly INamedTypeSymbol? _genericTask;
    private readonly INamedTypeSymbol? _valueTask;
    private readonly INamedTypeSymbol? _genericValueTask;
    private readonly INamedTypeSymbol? _asyncEnumerable;

    private KnownDataSourceTypes(
        INamedTypeSymbol? task,
        INamedTypeSymbol? genericTask,
        INamedTypeSymbol? valueTask,
        INamedTypeSymbol? genericValueTask,
        INamedTypeSymbol? asyncEnumerable)
    {
        _task = task;
        _genericTask = genericTask;
        _valueTask = valueTask;
        _genericValueTask = genericValueTask;
        _asyncEnumerable = asyncEnumerable;
    }

    public static KnownDataSourceTypes Create(Compilation compilation) =>
        new(
            compilation.GetTypeByMetadataName(WellKnownTypeNames.Task),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.GenericTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.ValueTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.GenericValueTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.AsyncEnumerable));

    /// <summary>
    /// Classifies the type a data source member exposes.
    /// </summary>
    /// <remarks>
    /// Synchronous collections are matched first on purpose. A type that implements both
    /// <c>IEnumerable&lt;T&gt;</c> and <c>IAsyncEnumerable&lt;T&gt;</c> kept the synchronous meaning
    /// before async sources existed, so matching async first would silently change how an already
    /// working data source is expanded.
    /// </remarks>
    public DataSourceClassification Classify(ITypeSymbol? memberType)
    {
        if (memberType is null)
        {
            return new DataSourceClassification(DataSourceShape.Sync, null);
        }

        if (IsSyncCollection(memberType))
        {
            return new DataSourceClassification(DataSourceShape.Sync, TryGetSyncElementType(memberType));
        }

        if (memberType is not INamedTypeSymbol namedType)
        {
            return new DataSourceClassification(DataSourceShape.Sync, null);
        }

        var asyncElementType = TryGetAsyncElementType(namedType);
        if (asyncElementType is not null)
        {
            return new DataSourceClassification(DataSourceShape.AsyncEnumerable, asyncElementType);
        }

        return ClassifyAwaitable(namedType);
    }

    /// <summary>
    /// Classifies a type that is awaited rather than enumerated.
    /// </summary>
    /// <remarks>
    /// The base chain is walked rather than the original definition compared once, because a type
    /// deriving from <c>Task&lt;TCollection&gt;</c> is still awaited as one. Matching only the exact
    /// definition classified it as synchronous, and the generator then emitted a synchronous
    /// provider that the runtime could not cast, reporting the source as missing with no diagnostic
    /// to explain it. The sibling test-method classifier walks the chain for the same reason.
    /// </remarks>
    private DataSourceClassification ClassifyAwaitable(INamedTypeSymbol namedType)
    {
        for (INamedTypeSymbol? current = namedType; current is not null; current = current.BaseType)
        {
            var isTask = Matches(current.OriginalDefinition, _genericTask);
            var isValueTask = Matches(current.OriginalDefinition, _genericValueTask);

            if (isTask || isValueTask)
            {
                var awaitedType = current.TypeArguments[0];
                if (!IsSyncCollection(awaitedType))
                {
                    return new DataSourceClassification(DataSourceShape.UnsupportedAwaitable, null);
                }

                return new DataSourceClassification(
                    isTask ? DataSourceShape.TaskOfCollection : DataSourceShape.ValueTaskOfCollection,
                    TryGetSyncElementType(awaitedType));
            }

            if (Matches(current, _task) || Matches(current, _valueTask))
            {
                return new DataSourceClassification(DataSourceShape.UnsupportedAwaitable, null);
            }
        }

        return new DataSourceClassification(DataSourceShape.Sync, null);
    }

    private ITypeSymbol? TryGetAsyncElementType(INamedTypeSymbol namedType)
    {
        if (_asyncEnumerable is null)
        {
            return null;
        }

        if (Matches(namedType.OriginalDefinition, _asyncEnumerable))
        {
            return namedType.TypeArguments[0];
        }

        foreach (var candidate in namedType.AllInterfaces)
        {
            if (Matches(candidate.OriginalDefinition, _asyncEnumerable))
            {
                return candidate.TypeArguments[0];
            }
        }

        return null;
    }

    private static bool Matches(ISymbol candidate, INamedTypeSymbol? known) =>
        known is not null && SymbolEqualityComparer.Default.Equals(candidate, known);

    private static bool IsSyncCollection(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return true;
        }

        if (type.SpecialType == SpecialType.System_Collections_IEnumerable)
        {
            return true;
        }

        foreach (var candidate in type.AllInterfaces)
        {
            if (candidate.SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the element type of a synchronous collection, or <c>null</c> when only the non-generic
    /// <c>IEnumerable</c> is implemented and the element type is therefore not statically known.
    /// </summary>
    public static ITypeSymbol? TryGetSyncElementType(ITypeSymbol? type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return array.ElementType;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        if (IsGenericEnumerable(namedType))
        {
            return namedType.TypeArguments[0];
        }

        foreach (var candidate in namedType.AllInterfaces)
        {
            if (IsGenericEnumerable(candidate))
            {
                return candidate.TypeArguments[0];
            }
        }

        return null;
    }

    private static bool IsGenericEnumerable(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
}
