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

        var compilingAssembly = knownDataSourceTypes.CompilingAssembly;
        var members = GetCandidateMembers(typeSymbol, memberName, compilingAssembly);

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
    /// <c>Rows()</c> stay a candidate beside a derived <c>Rows(CancellationToken)</c>: that
    /// overload is not applicable to a call supplying no arguments, so it never reduces the base
    /// one away.
    /// </para>
    /// <para>
    /// A base method <em>is</em> dropped when a nearer level declares one that is applicable to the
    /// same call, whether or not the signatures match and whether or not it is static. C# reduces
    /// the applicable candidates to those declared in the most derived type, so a derived
    /// <c>Rows(int count = 1)</c> or <c>Rows(params int[])</c> is what <c>Derived.Rows()</c> calls
    /// even though a base <c>Rows()</c> exists. Validating the base member there would classify one
    /// member's rows while the emitted call ran another's -- silently, with no diagnostic and no
    /// build failure. Dropping it instead leaves the source unbound and <c>NU0003</c> names it.
    /// </para>
    /// <para>
    /// A base member C# member lookup cannot see from a derived type is skipped, because it neither
    /// binds nor hides: <c>private</c> always, and <c>internal</c> or <c>private protected</c>
    /// declared in another assembly that grants no <c>InternalsVisibleTo</c>. Letting one win would
    /// report <c>NU0020</c> for a name that in fact resolves further up the chain and compiles. A
    /// <c>private</c> member on <paramref name="typeSymbol"/> itself is still collected and refused
    /// by <see cref="GeneratedRegistryAccess.CanReachMember"/> -- it is the member the user named,
    /// so naming it in a diagnostic beats silently binding a different one.
    /// </para>
    /// </remarks>
    public static ImmutableArray<ISymbol> GetCandidateMembers(
        INamedTypeSymbol typeSymbol,
        string memberName,
        IAssemblySymbol? compilingAssembly)
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

        // Which of the two calls the generator can emit a nearer level already answers. Once a
        // level does, every base method applicable to that same call is reduced away by C#, so
        // keeping one would validate a member the emitted call never reaches.
        var claimedNoArgument = DeclaresApplicableWithoutArguments(members);
        var claimedToken = DeclaresApplicableWithCancellationToken(members);

        for (var baseType = typeSymbol.BaseType;
            baseType is not null && baseType.SpecialType != SpecialType.System_Object;
            baseType = baseType.BaseType)
        {
            var inherited = baseType.GetMembers(memberName)
                .RemoveAll(member => !IsVisibleToDerivedType(member, compilingAssembly));

            if (claimedNoArgument || claimedToken)
            {
                inherited = inherited.RemoveAll(member =>
                    member is IMethodSymbol method &&
                    ((claimedNoArgument && IsApplicableWithoutArguments(method)) ||
                        (claimedToken && IsApplicableWithCancellationToken(method))));
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
            claimedNoArgument |= DeclaresApplicableWithoutArguments(inherited);
            claimedToken |= DeclaresApplicableWithCancellationToken(inherited);
        }

        return builder?.ToImmutable() ?? members;
    }

    /// <summary>
    /// Reports whether C# member lookup from a type derived from the declaring type can see
    /// <paramref name="member"/> at all.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="GeneratedRegistryAccess.CanReachMember"/>, which asks whether the
    /// generated registry can name a member. A <c>protected</c> base member is invisible to the
    /// registry but perfectly visible to the derived test class, so it still hides and still binds;
    /// reporting it as <c>NU0020</c> is the right answer there. One that is invisible to the derived
    /// class does not hide anything, and skipping it is what lets the accessible ancestor bind.
    /// </remarks>
    private static bool IsVisibleToDerivedType(ISymbol member, IAssemblySymbol? compilingAssembly)
    {
        switch (member.DeclaredAccessibility)
        {
            case Accessibility.Private:
                return false;

            // Both require the consuming code to share the declaring assembly. Across an assembly
            // boundary without InternalsVisibleTo, neither is in scope for the derived class, so
            // neither can hide a public ancestor.
            case Accessibility.Internal:
            case Accessibility.ProtectedAndInternal:
                return compilingAssembly is null ||
                    SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, compilingAssembly) ||
                    member.ContainingAssembly?.GivesAccessTo(compilingAssembly) == true;

            default:
                return true;
        }
    }

    /// <summary>
    /// Reports whether the method can be called with no arguments at all, which is the call the
    /// generator emits for the parameterless shape.
    /// </summary>
    /// <remarks>
    /// Optional parameters and a trailing <c>params</c> array both count: C# fills them in, so
    /// <c>Rows(int count = 1)</c> and <c>Rows(params int[] values)</c> each answer <c>Rows()</c>.
    /// A generic method does not, because the emitted call names no type argument and there is
    /// nothing to infer one from.
    /// </remarks>
    private static bool IsApplicableWithoutArguments(IMethodSymbol method) =>
        method.Arity == 0 && ParametersCanBeOmittedFrom(method, 0);

    /// <summary>
    /// The same test for the cancellation-aware shape, where the emitted call supplies the token
    /// and nothing else.
    /// </summary>
    private static bool IsApplicableWithCancellationToken(IMethodSymbol method) =>
        method.Arity == 0 &&
        method.Parameters.Length >= 1 &&
        IsCancellationToken(method.Parameters[0]) &&
        ParametersCanBeOmittedFrom(method, 1);

    private static bool ParametersCanBeOmittedFrom(IMethodSymbol method, int firstOmitted)
    {
        for (var i = firstOmitted; i < method.Parameters.Length; i++)
        {
            if (!method.Parameters[i].IsOptional && !method.Parameters[i].IsParams)
            {
                return false;
            }
        }

        return true;
    }

    private static bool DeclaresApplicableWithoutArguments(ImmutableArray<ISymbol> members)
    {
        foreach (var member in members)
        {
            if (member is IMethodSymbol method && IsApplicableWithoutArguments(method))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresApplicableWithCancellationToken(ImmutableArray<ISymbol> members)
    {
        foreach (var member in members)
        {
            if (member is IMethodSymbol method && IsApplicableWithCancellationToken(method))
            {
                return true;
            }
        }

        return false;
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

    private static ImmutableArray<ISymbol>.Builder CreateBuilderFrom(ImmutableArray<ISymbol> members)
    {
        var builder = ImmutableArray.CreateBuilder<ISymbol>();
        builder.AddRange(members);
        return builder;
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
