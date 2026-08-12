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
    /// Returns the members of the nearest type in the base chain that declares
    /// <paramref name="memberName"/>, and nothing else.
    /// </summary>
    /// <remarks>
    /// The contract for inherited data sources, and deliberately narrower than C# member lookup:
    /// <b>the nearest declaring level wins, or the source is diagnosed</b>. Whichever type first
    /// declares the name -- the type the attribute points at, or the closest base that declares it
    /// -- is the only type considered. If nothing on that level binds, for any reason at all, the
    /// source is reported rather than resolved against a farther level.
    /// <para>
    /// C# does more than this. It accumulates method overloads across the chain, then reduces the
    /// applicable ones to the most derived declaring type, and it excludes members the calling code
    /// cannot see before any of that. Modeling those rules here was tried and abandoned: each round
    /// of review found another slice of the specification the model got wrong, and every wrong
    /// slice had the same shape -- the resolver validating and classifying one member while the
    /// emitted call ran another, silently. This contract makes that failure structurally
    /// impossible. The emitted access names the nearest declaring level, so no nearer binding can
    /// exist for the compiler to prefer.
    /// </para>
    /// <para>
    /// The cost is that a base member is unreachable once any nearer type declares the same name,
    /// even where C# would still bind the base one -- a derived <c>Rows(CancellationToken)</c> over
    /// a base <c>Rows()</c>, for instance. That case is a diagnostic now, and the fix is to declare
    /// the member on the derived type or rename one of them. Loud and mechanical beats a silent
    /// mismatch between the rows validated and the rows run.
    /// </para>
    /// <para>
    /// Interfaces are not walked, because a static interface member cannot be named through an
    /// implementing type. The walk stops short of <c>object</c>, whose static members can never
    /// bind: admitting them would turn <c>NU0003</c> on a misspelled name into a level that
    /// declares the name and then supplies nothing.
    /// </para>
    /// </remarks>
    public static ImmutableArray<ISymbol> GetCandidateMembers(INamedTypeSymbol typeSymbol, string memberName)
    {
        for (var level = typeSymbol;
            level is not null && level.SpecialType != SpecialType.System_Object;
            level = level.BaseType)
        {
            var members = level.GetMembers(memberName);
            if (!members.IsEmpty)
            {
                return members;
            }
        }

        return ImmutableArray<ISymbol>.Empty;
    }

    /// <summary>
    /// Finds a farther base type that also declares <paramref name="memberName"/>, past the nearest
    /// one that does.
    /// </summary>
    /// <remarks>
    /// Only asked once a source has already failed to bind, so that the diagnostic can say why the
    /// obvious reading of the code is not the one that applies. The nearest declaring level is the
    /// only one the contract considers, and a user looking at a base class that plainly declares the
    /// member has no way to guess that from a message about the member being missing or
    /// unreachable. Naming the base type turns the report into the fix: point the attribute at it
    /// with <c>MemberType</c>.
    /// </remarks>
    public static INamedTypeSymbol? FindShadowedDeclaringType(INamedTypeSymbol typeSymbol, string memberName)
    {
        var foundNearest = false;

        for (var level = typeSymbol;
            level is not null && level.SpecialType != SpecialType.System_Object;
            level = level.BaseType)
        {
            if (level.GetMembers(memberName).IsEmpty)
            {
                continue;
            }

            if (foundNearest)
            {
                return level;
            }

            foundNearest = true;
        }

        return null;
    }

    /// <summary>
    /// Reports whether a member has one of the two shapes a data source can be emitted from: read
    /// directly, or called with no arguments, or called with the discovery token.
    /// </summary>
    /// <remarks>
    /// The analyzers use this for the "does a usable member of this name exist at all" test that
    /// precedes <see cref="Resolve"/>. A method that requires arguments is not usable however it is
    /// declared -- neither the generated call nor the reflection fallback supplies any -- so
    /// admitting it there would leave a source that binds nothing, reports nothing, and supplies
    /// nothing.
    /// </remarks>
    public static bool HasBindableShape(ISymbol member) => member switch
    {
        IMethodSymbol { Arity: 0 } method =>
            method.Parameters.Length == 0 ||
            (method.Parameters.Length == 1 && IsCancellationToken(method.Parameters[0])),
        IPropertySymbol or IFieldSymbol => true,
        _ => false
    };

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
