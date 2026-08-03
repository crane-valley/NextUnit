using Microsoft.CodeAnalysis;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// The data source member a <c>[TestData]</c> attribute binds to.
/// </summary>
internal readonly struct ResolvedDataSourceMember
{
    public ResolvedDataSourceMember(ISymbol? symbol, ITypeSymbol? memberType, bool acceptsCancellationToken)
    {
        Symbol = symbol;
        MemberType = memberType;
        AcceptsCancellationToken = acceptsCancellationToken;
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

            switch (member)
            {
                case IMethodSymbol { Parameters.Length: 0 } method:
                    return new ResolvedDataSourceMember(method, method.ReturnType, false);

                case IPropertySymbol property:
                    return new ResolvedDataSourceMember(property, property.Type, false);

                case IFieldSymbol field:
                    return new ResolvedDataSourceMember(field, field.Type, false);
            }
        }

        // Second pass admits the new shape. A cancellation token is only meaningful for an
        // asynchronous source: the synchronous provider delegate takes no arguments, so there would
        // be no token to pass and the call could not be emitted.
        foreach (var member in members)
        {
            if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method ||
                !IsCancellationToken(method.Parameters[0]))
            {
                continue;
            }

            if (knownDataSourceTypes.Classify(method.ReturnType).IsAsync)
            {
                return new ResolvedDataSourceMember(method, method.ReturnType, true);
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
