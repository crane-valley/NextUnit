using System.Text;
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
        INamedTypeSymbol? asyncEnumerable,
        IAssemblySymbol? compilingAssembly)
    {
        _task = task;
        _genericTask = genericTask;
        _valueTask = valueTask;
        _genericValueTask = genericValueTask;
        _asyncEnumerable = asyncEnumerable;
        CompilingAssembly = compilingAssembly;
    }

    /// <summary>
    /// Gets the assembly being compiled, which is where the generated registry lands.
    /// </summary>
    /// <remarks>
    /// Carried here rather than passed alongside because every caller that resolves a data source
    /// member already threads this value through, and because it is resolved once per compilation
    /// for the same reason the type symbols are.
    /// </remarks>
    public IAssemblySymbol? CompilingAssembly { get; }

    public static KnownDataSourceTypes Create(Compilation compilation) =>
        new(
            compilation.GetTypeByMetadataName(WellKnownTypeNames.Task),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.GenericTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.ValueTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.GenericValueTask),
            compilation.GetTypeByMetadataName(WellKnownTypeNames.AsyncEnumerable),
            compilation.Assembly);

    /// <summary>
    /// Reports whether the type implements <c>IAsyncEnumerable&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Asked separately from <see cref="Classify"/> because a type implementing both element
    /// interfaces classifies as synchronous, and telling that case apart from a plainly synchronous
    /// collection is what <c>NU0021</c> reports on.
    /// </remarks>
    public bool ImplementsAsyncEnumerable(ITypeSymbol? type) =>
        type is INamedTypeSymbol namedType && TryGetAsyncElementType(namedType) is not null;

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

        ITypeSymbol? selected = null;
        foreach (var candidate in namedType.AllInterfaces)
        {
            if (Matches(candidate.OriginalDefinition, _asyncEnumerable))
            {
                selected = SelectRowType(selected, candidate.TypeArguments[0]);
            }
        }

        return selected;
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

        ITypeSymbol? selected = null;
        foreach (var candidate in namedType.AllInterfaces)
        {
            if (IsGenericEnumerable(candidate))
            {
                selected = SelectRowType(selected, candidate.TypeArguments[0]);
            }
        }

        return selected;
    }

    /// <summary>
    /// Reports whether the type is <c>NextUnit.TestDataRow&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The namespace is matched by walking to the global namespace rather than by comparing
    /// <c>ToDisplayString</c>, which allocates on every call: this runs for every row type the
    /// analyzers validate, and <see cref="DataSourceMemberResolver"/> matches
    /// <c>CancellationToken</c> the same way for the same reason. The metadata name carries the
    /// generic arity, so <c>TestDataRow</c> with any other arity does not match.
    /// </remarks>
    public static bool IsTestDataRow(ITypeSymbol type) =>
        type is INamedTypeSymbol
        {
            IsGenericType: true,
            MetadataName: NextUnitAttributeNames.MetadataNames.TestDataRow,
            ContainingNamespace:
            {
                Name: NextUnitAttributeNames.Namespace,
                ContainingNamespace.IsGlobalNamespace: true
            }
        };

    /// <summary>
    /// Picks between two element types when a source type implements the same element interface
    /// more than once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>AllInterfaces</c> has no documented order, so taking the first match made the row type of
    /// a source implementing, say, both <c>IEnumerable&lt;object[]&gt;</c> and
    /// <c>IEnumerable&lt;TestDataRow&lt;T&gt;&gt;</c> a property of symbol enumeration rather than of
    /// the source, and <c>NU0009</c> could accept or reject the same code between compilations.
    /// </para>
    /// <para>
    /// The rule is: <c>TestDataRow&lt;T&gt;</c> wins, because it is the more specific contract -- it
    /// carries the row's metadata as well as its values, and a source that offers it declared the
    /// typed shape deliberately. Remaining ties are broken by ordinal comparison of the fully
    /// qualified element type name, and then of the declaring assembly's identity, since two
    /// assemblies reached through <c>extern alias</c> can contribute the same qualified name. Both
    /// keys depend on nothing but the types themselves. The comparison only runs once a second
    /// candidate appears, so the ordinary single-interface source formats no display string.
    /// </para>
    /// </remarks>
    private static ITypeSymbol SelectRowType(ITypeSymbol? selected, ITypeSymbol candidate)
    {
        if (selected is null)
        {
            return candidate;
        }

        var selectedIsRow = IsTestDataRow(selected);
        var candidateIsRow = IsTestDataRow(candidate);

        if (selectedIsRow != candidateIsRow)
        {
            return candidateIsRow ? candidate : selected;
        }

        var order = string.CompareOrdinal(
            candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            selected.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        if (order == 0)
        {
            order = string.CompareOrdinal(AssemblyKey(candidate), AssemblyKey(selected));
        }

        return order < 0 ? candidate : selected;
    }

    /// <summary>
    /// Builds the second sort key: the declaring assemblies of a type and of everything it is
    /// composed from.
    /// </summary>
    /// <remarks>
    /// A fully qualified name carries no assembly, so two assemblies reached through
    /// <c>extern alias</c> can contribute the same one. The key is structural rather than the outer
    /// type's assembly alone, because the difference can sit in an array element or a type argument
    /// -- <c>List&lt;A::Row&gt;</c> and <c>List&lt;B::Row&gt;</c> are both declared by the assembly
    /// that declares <c>List</c>. The containing type chain is walked for its type arguments and not
    /// for its assembly, which is always the assembly of the type it contains:
    /// <c>Outer&lt;A::Row&gt;.Inner</c> carries its distinguishing argument one level up.
    /// </remarks>
    private static string AssemblyKey(ITypeSymbol type)
    {
        var builder = new StringBuilder();
        AppendAssemblyKey(type, builder);
        return builder.ToString();
    }

    private static void AppendAssemblyKey(ITypeSymbol type, StringBuilder builder)
    {
        if (type is IArrayTypeSymbol array)
        {
            AppendAssemblyKey(array.ElementType, builder);
            return;
        }

        if (type is INamedTypeSymbol namedType)
        {
            builder.Append(namedType.ContainingAssembly?.Identity.GetDisplayName());

            for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
            {
                foreach (var typeArgument in current.TypeArguments)
                {
                    AppendAssemblyKey(typeArgument, builder);
                }
            }
        }

        // A separator after every node, so that two different shapes cannot flatten to one key.
        builder.Append(';');
    }

    private static bool IsGenericEnumerable(INamedTypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
}
