using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// The data source member a <c>[TestData]</c> attribute binds to.
/// </summary>
internal readonly struct ResolvedDataSourceMember
{
    public ResolvedDataSourceMember(
        ISymbol? symbol,
        ITypeSymbol? memberType,
        bool acceptsCancellationToken,
        DataSourceBindingIssue issue = DataSourceBindingIssue.None)
    {
        Symbol = symbol;
        MemberType = memberType;
        AcceptsCancellationToken = acceptsCancellationToken;
        Issue = issue;
    }

    /// <summary>
    /// Gets the bound member, or <c>null</c> when no member with that name can be invoked.
    /// </summary>
    public ISymbol? Symbol { get; }

    /// <summary>
    /// Gets the type the bound member exposes, or <c>null</c> when nothing is bound.
    /// </summary>
    public ITypeSymbol? MemberType { get; }

    /// <summary>
    /// Gets a value indicating whether the bound member is a method taking the discovery
    /// cancellation token.
    /// </summary>
    public bool AcceptsCancellationToken { get; }

    /// <summary>
    /// Gets why the generator declines to bind the member, or
    /// <see cref="DataSourceBindingIssue.None"/> when it binds.
    /// </summary>
    /// <remarks>
    /// A member carrying an issue is still returned, so that the analyzers can name it in a
    /// diagnostic. Callers that emit code have to treat it as unbound.
    /// </remarks>
    public DataSourceBindingIssue Issue { get; }
}

/// <summary>
/// Picks which member a <c>[TestData]</c> name binds to.
/// </summary>
/// <remarks>
/// Shared by the source generator and the analyzers on purpose. If they disagreed, the analyzer
/// would validate one overload while the generator emitted another, and the resulting diagnostic
/// would describe code that never runs.
/// </remarks>
internal static class DataSourceMemberResolver
{
    public static ResolvedDataSourceMember Resolve(
        INamedTypeSymbol? typeSymbol,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        if (typeSymbol is null)
        {
            return default;
        }

        var members = GetCandidateMembers(typeSymbol, memberName);
        var compilingAssembly = knownDataSourceTypes.CompilingAssembly;

        // First pass reproduces the pre-async precedence exactly: the first static parameterless
        // method, property, or field wins. Running it before the cancellation-aware pass is what
        // keeps a suite that already had both Rows() and Rows(CancellationToken) bound to Rows(),
        // so upgrading cannot silently switch which data a test enumerates.
        foreach (var member in members)
        {
            if (!member.IsStatic)
            {
                continue;
            }

            // Arity is part of the test: the generator emits an unadorned Rows() call, so a generic
            // Rows<T>() could not be emitted even though it takes no value parameters. Skipping it
            // also matches C# member lookup, which binds the non-generic overload for that call.
            ITypeSymbol? memberType = member switch
            {
                IMethodSymbol { Parameters.Length: 0, Arity: 0 } method => method.ReturnType,
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (memberType is null)
            {
                continue;
            }

            // An unreachable member wins the pass and is then refused, rather than being skipped so
            // that a later overload can win it. Skipping would silently move the binding to data the
            // user did not ask for, which is exactly what the parameterless-first rule above exists
            // to prevent; refusing it names the accessibility as the thing to fix.
            return GeneratedRegistryAccess.CanReachMember(member, compilingAssembly)
                ? new ResolvedDataSourceMember(member, memberType, false)
                : new ResolvedDataSourceMember(
                    member,
                    memberType,
                    false,
                    DataSourceBindingIssue.MemberNotAccessible);
        }

        // Second pass admits the new shape. A cancellation token is only meaningful for an
        // asynchronous source: the synchronous provider delegate takes no arguments, so there would
        // be no token to pass and the call could not be emitted.
        foreach (var member in members)
        {
            if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 1, Arity: 0 } method ||
                !IsCancellationToken(method.Parameters[0]))
            {
                continue;
            }

            var classification = knownDataSourceTypes.Classify(method.ReturnType);

            // Accessibility is judged before the shape, as it is in the first pass, so that one
            // member never reports one rule on the way to another: a private member of any shape
            // reports NU0020 rather than reporting the shape first and the accessibility only once
            // the shape is fixed.
            if (!GeneratedRegistryAccess.CanReachMember(method, compilingAssembly))
            {
                if (classification.IsAsync ||
                    classification.Shape == DataSourceShape.UnsupportedAwaitable ||
                    knownDataSourceTypes.ImplementsAsyncEnumerable(method.ReturnType))
                {
                    return new ResolvedDataSourceMember(
                        method,
                        method.ReturnType,
                        false,
                        DataSourceBindingIssue.MemberNotAccessible);
                }

                // A plainly synchronous return type was never a candidate here, accessible or not,
                // so it keeps falling through to the next overload rather than being claimed.
                continue;
            }

            if (classification.IsAsync)
            {
                return new ResolvedDataSourceMember(method, method.ReturnType, true);
            }

            // An awaitable that supplies no rows is still returned, without being marked bindable.
            // Reporting it is the whole point of NU0014, and staying silent here would leave the
            // user with only a parameter-count failure from the runtime reflection fallback. The
            // generator emits no provider for this shape, so returning it changes nothing it emits.
            if (classification.Shape == DataSourceShape.UnsupportedAwaitable)
            {
                return new ResolvedDataSourceMember(method, method.ReturnType, false);
            }

            // A synchronous classification here means the return type implements IEnumerable<T> as
            // well as IAsyncEnumerable<T> and the sync-first rule picked the synchronous meaning.
            // The member is unbindable either way -- the synchronous provider has no token to pass --
            // so it is returned as an issue instead of falling through to nothing, which used to
            // leave a parameter-count failure from the runtime reflection fallback as the only
            // symptom. A return type that is only IEnumerable<T> stays unbound and unreported: the
            // token was never meaningful there, and that shape predates asynchronous sources.
            if (knownDataSourceTypes.ImplementsAsyncEnumerable(method.ReturnType))
            {
                return new ResolvedDataSourceMember(
                    method,
                    method.ReturnType,
                    false,
                    DataSourceBindingIssue.CancellationTokenOnSynchronousSource);
            }
        }

        return default;
    }

    /// <summary>
    /// Collects every member of that name on <paramref name="typeSymbol"/> and its base types,
    /// most-derived first.
    /// </summary>
    /// <remarks>
    /// <c>INamedTypeSymbol.GetMembers(string)</c> stops at the declaring type, so a data
    /// source declared on a base test class used to resolve to nothing even though C# binds
    /// <c>Derived.Rows</c> without complaint and the emitted access would have compiled.
    /// <para>
    /// Most-derived first is what makes a derived member shadow a base member of the same name,
    /// which is the order C# itself picks. Both passes in <see cref="Resolve"/> run over the whole
    /// flattened chain rather than per type, so a base <c>Rows()</c> still beats a derived
    /// <c>Rows(CancellationToken)</c> -- the same parameterless-first precedence C# overload
    /// resolution applies to a call that supplies no arguments.
    /// </para>
    /// <para>
    /// Interfaces are deliberately not walked. A static interface member cannot be named through an
    /// implementing type, so binding one would emit access that does not compile.
    /// </para>
    /// <para>
    /// The walk stops short of <c>object</c>. Nothing declared there can bind -- neither
    /// <c>Equals</c> nor <c>ReferenceEquals</c> is parameterless -- so admitting its members would
    /// only turn <c>NU0003</c> on a misspelled name into a source that reports nothing and supplies
    /// nothing.
    /// </para>
    /// <para>
    /// C# hiding is applied as the chain is walked, because the generator emits <c>Derived.Rows</c>
    /// and that name has to mean here what it means to the compiler. A member that is not a method
    /// hides everything of that name below it, and a method hides a base member that is not one, so
    /// a base property is dropped once any derived type declares a method of the same name --
    /// binding it would emit a property read for a name the compiler reads as a method group, which
    /// does not compile. Methods accumulate across levels instead, which is what lets a base
    /// <c>Rows()</c> stay a candidate beside a derived <c>Rows(CancellationToken)</c> -- but a base
    /// method whose signature a nearer declaration repeats is dropped, static or not, because that
    /// nearer declaration is the one the compiler binds.
    /// </para>
    /// <para>
    /// A base type's <c>private</c> members are skipped, because C# member lookup never sees them
    /// from a derived type: they neither bind nor hide, so letting one win would report
    /// <c>NU0020</c> for a name that in fact resolves further up the chain. A <c>private</c> member
    /// on <paramref name="typeSymbol"/> itself is still collected and refused by
    /// <see cref="GeneratedRegistryAccess.CanReachMember"/> -- it is the member the user named, so
    /// naming it in a diagnostic beats silently binding a different one.
    /// </para>
    /// </remarks>
    public static ImmutableArray<ISymbol> GetCandidateMembers(INamedTypeSymbol typeSymbol, string memberName)
    {
        var members = typeSymbol.GetMembers(memberName);

        // A declaration that is not a method ends the walk: it hides every same-named member below
        // it, so nothing further up the chain can be a candidate.
        if (DeclaresNonMethod(members))
        {
            return members;
        }

        // The chain usually contributes nothing, so the declared members are returned as they are
        // and the builder is only paid for when a base type actually carries the name.
        ImmutableArray<ISymbol>.Builder? builder = null;
        var sawMethod = !members.IsEmpty;

        // Every method a nearer level declared. Reaching here means the declared members are all
        // methods, and only methods are ever appended, so this stays a method list.
        var claimed = members;

        for (var baseType = typeSymbol.BaseType;
            baseType is not null && baseType.SpecialType != SpecialType.System_Object;
            baseType = baseType.BaseType)
        {
            var inherited = baseType.GetMembers(memberName)
                .RemoveAll(static member => member.DeclaredAccessibility == Accessibility.Private);

            // A method is hidden by a nearer declaration of the same signature, whether or not that
            // one is static. Keeping the base method would bind a call the compiler resolves to the
            // derived declaration instead: a derived instance Rows() makes the emitted Derived.Rows()
            // a CS0120 in generated code, which is worse than the NU0003 that not binding produces.
            if (!claimed.IsEmpty)
            {
                inherited = inherited.RemoveAll(member =>
                    member is IMethodSymbol method && IsSignatureClaimed(claimed, method));
            }

            if (inherited.IsEmpty)
            {
                continue;
            }

            // Hidden by the methods already collected, and hiding whatever lies below it, so the
            // walk ends here with nothing added.
            if (sawMethod && DeclaresNonMethod(inherited))
            {
                break;
            }

            builder ??= CreateBuilderFrom(members);
            builder.AddRange(inherited);

            if (DeclaresNonMethod(inherited))
            {
                break;
            }

            sawMethod = true;
            claimed = claimed.AddRange(inherited);
        }

        return builder?.ToImmutable() ?? members;
    }

    private static bool DeclaresNonMethod(ImmutableArray<ISymbol> members)
    {
        foreach (var member in members)
        {
            if (member is not IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSignatureClaimed(ImmutableArray<ISymbol> claimed, IMethodSymbol method)
    {
        foreach (var member in claimed)
        {
            if (member is IMethodSymbol nearer && HasSameSignature(nearer, method))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares two methods the way C# hiding does: by arity and parameter list, ignoring the
    /// return type and whether either one is static.
    /// </summary>
    private static bool HasSameSignature(IMethodSymbol left, IMethodSymbol right)
    {
        if (left.Arity != right.Arity || left.Parameters.Length != right.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Parameters.Length; i++)
        {
            if (left.Parameters[i].RefKind != right.Parameters[i].RefKind ||
                !SymbolEqualityComparer.Default.Equals(left.Parameters[i].Type, right.Parameters[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<ISymbol>.Builder CreateBuilderFrom(ImmutableArray<ISymbol> members)
    {
        var builder = ImmutableArray.CreateBuilder<ISymbol>();
        builder.AddRange(members);
        return builder;
    }

    /// <summary>
    /// Matches a by-value <c>System.Threading.CancellationToken</c> parameter.
    /// </summary>
    /// <remarks>
    /// The reference kind is part of the test, not a detail: the generator emits the token as a
    /// plain value argument, so a <c>ref</c> or <c>out</c> parameter would produce code that does
    /// not compile. Rejecting it here leaves the member unbound, which is the same outcome as
    /// before asynchronous sources existed.
    /// <para>
    /// Matched on symbol identity rather than <c>ToDisplayString</c> because this runs for every
    /// candidate member: formatting a display string allocates, and this is an analyzer hot path.
    /// </para>
    /// </remarks>
    private static bool IsCancellationToken(IParameterSymbol parameter) =>
        parameter.RefKind == RefKind.None &&
        parameter.Type is INamedTypeSymbol
        {
            Name: "CancellationToken",
            ContainingNamespace:
            {
                Name: "Threading",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            }
        };
}
