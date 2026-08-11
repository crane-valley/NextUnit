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

        var members = typeSymbol.GetMembers(memberName);
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
