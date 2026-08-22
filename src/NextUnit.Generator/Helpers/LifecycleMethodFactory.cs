using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NextUnit.CodeAnalysis.Shared;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Turns a <c>[Before]</c> or <c>[After]</c> method symbol into a <see cref="LifecycleMethodDescriptor"/>.
/// </summary>
/// <remarks>
/// One definition of "what is a hook", shared by the two syntax providers, which see the hooks
/// declared in this compilation, and by the base-class walk, which sees the ones a test class
/// inherits including from referenced assemblies. Two readers would eventually disagree about which
/// method is a hook, and the disagreement would show up as a hook that runs on one path and not the
/// other.
/// </remarks>
internal static class LifecycleMethodFactory
{
    /// <summary>
    /// Format for the override chain key.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than reusing <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>,
    /// which sets no member options and therefore prints a method as its bare name: every hook
    /// called <c>Setup</c> would answer the same key, and a <c>new</c> method would be collapsed
    /// into the base hook it hides. The containing type and the parameter types are what make the
    /// key identify one method.
    /// </remarks>
    private static readonly SymbolDisplayFormat _overrideRootFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>
    /// Builds the descriptor for one direction of one hook, or returns <c>null</c> when the method
    /// declares no scope in that direction.
    /// </summary>
    /// <param name="methodSymbol">The candidate hook.</param>
    /// <param name="invocationTypeName">
    /// The type the emitted delegate casts to. The declaring type as the test class sees it, which
    /// for an inherited hook on an open generic base is the constructed form.
    /// </param>
    /// <param name="knownTypes">The return type classifier for this compilation.</param>
    /// <param name="semanticModel">
    /// The binder the emitted name is checked against, which decides both accessibility and whether
    /// the name the registry has to write reaches the declaring type at all.
    /// </param>
    /// <param name="isBefore">Whether to read <c>[Before]</c> rather than <c>[After]</c>.</param>
    public static LifecycleMethodDescriptor? TryCreate(
        IMethodSymbol methodSymbol,
        string invocationTypeName,
        KnownReturnTypes knownTypes,
        SemanticModel? semanticModel,
        bool isBefore)
    {
        var scopes = AttributeHelper.GetLifecycleScopes(
            methodSymbol,
            isBefore ? NextUnitAttributeNames.Before : NextUnitAttributeNames.After);

        if (scopes.IsDefaultOrEmpty)
        {
            return null;
        }

        return new LifecycleMethodDescriptor(
            AttributeHelper.GetFullyQualifiedTypeName(methodSymbol.ContainingType.OriginalDefinition),
            invocationTypeName,
            methodSymbol.Name,
            GetOverrideRootId(methodSymbol),
            IsReachableFrom(methodSymbol, invocationTypeName, semanticModel),
            methodSymbol.MethodKind == MethodKind.ExplicitInterfaceImplementation,
            isBefore ? scopes : EquatableArray<int>.Empty,
            isBefore ? EquatableArray<int>.Empty : scopes,
            methodSymbol.IsStatic,
            GetReturnKind(methodSymbol, knownTypes),
            HasTrailingCancellationToken(methodSymbol));
    }

    /// <summary>
    /// Collects the hooks <paramref name="typeSymbol"/> inherits from its base classes, base-most
    /// first, with the hooks of one class in declaration order.
    /// </summary>
    /// <remarks>
    /// <c>System.Object</c> ends the walk: nothing above a user test hierarchy can carry a NextUnit
    /// attribute, and stopping there keeps the emitted registry from depending on the framework
    /// members of every base type. Interfaces are not walked -- a default interface method hook
    /// would have no single declaration order across the implemented interfaces, and C# does not
    /// give the registry a way to call one without naming the interface.
    /// <para>
    /// An error type ends it too, and no level is visited twice. A circular base declaration is
    /// <c>CS0146</c> and Roslyn hands back an error type rather than a cycle, so neither guard fires
    /// on code the compiler accepted; both are here because a generator runs against half-typed code
    /// on every keystroke, where the graph is whatever error recovery produced, and the cost of
    /// trusting the invariant is an IDE that stops responding.
    /// </para>
    /// </remarks>
    public static EquatableArray<LifecycleMethodDescriptor> CollectInherited(
        INamedTypeSymbol typeSymbol,
        KnownReturnTypes knownTypes,
        SemanticModel? semanticModel)
    {
        var levels = new List<ImmutableArray<LifecycleMethodDescriptor>>();

        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        for (var baseType = typeSymbol.BaseType;
             baseType is not null &&
                 baseType.SpecialType != SpecialType.System_Object &&
                 baseType.TypeKind != TypeKind.Error &&
                 visited.Add(baseType);
             baseType = baseType.BaseType)
        {
            levels.Add(CollectDeclared(baseType, knownTypes, semanticModel));
        }

        if (levels.Count == 0)
        {
            return EquatableArray<LifecycleMethodDescriptor>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<LifecycleMethodDescriptor>();

        // Reversed so the array reads base-most first, which is the order [Before] hooks run in and
        // the order the emitter reverses for [After].
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            builder.AddRange(levels[i]);
        }

        return new EquatableArray<LifecycleMethodDescriptor>(builder.ToImmutable());
    }

    private static ImmutableArray<LifecycleMethodDescriptor> CollectDeclared(
        INamedTypeSymbol type,
        KnownReturnTypes knownTypes,
        SemanticModel? semanticModel)
    {
        var invocationTypeName = AttributeHelper.GetFullyQualifiedTypeName(type);
        var builder = ImmutableArray.CreateBuilder<LifecycleMethodDescriptor>();

        foreach (var member in type.GetMembers())
        {
            // Explicit interface implementations are collected too, even though the registry can
            // never call one. Skipping them here would drop an attributed hook without a word,
            // which is the failure this walk exists to remove; collecting them lets NEXTUNIT017
            // name the declaration a derived class inherits.
            //
            // The walk is not what keeps that rule honest, though, and no filter here could be: a
            // compilation imports metadata with MetadataImportOptions.Public by default, so an
            // explicit implementation on a base class in a *referenced* assembly is not in
            // GetMembers() at all, and raising the import options is a compilation-level setting a
            // generator does not own. NEXTUNIT017 therefore also fires in the assembly that
            // declares the hook, where it is still in source, so the shape cannot reach a consumer
            // that has no way to see it.
            if (member is not IMethodSymbol
                {
                    MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation
                } method)
            {
                continue;
            }

            // [Before] and [After] are read separately, and in that order, so a method carrying both
            // yields two descriptors exactly as the two syntax providers produce for a hook declared
            // on the test class itself.
            var before = TryCreate(method, invocationTypeName, knownTypes, semanticModel, isBefore: true);
            if (before is not null)
            {
                builder.Add(before);
            }

            var after = TryCreate(method, invocationTypeName, knownTypes, semanticModel, isBefore: false);
            if (after is not null)
            {
                builder.Add(after);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Whether the generated registry can call the hook.
    /// </summary>
    /// <remarks>
    /// Accessibility is the shared rule every emission site asks, and it is not the whole question
    /// here. The registry has to write the declaring type as a <c>global::</c>-rooted name in a cast,
    /// and a public type can still fail that: a reference brought in solely under an
    /// <c>extern alias</c> is invisible to that name, and so is a type argument of the constructed
    /// base. A derived test class can name such a base -- C# has the alias in scope, the generated
    /// file does not -- so the name the registry would emit is bound and compared against the type
    /// it is meant to reach, which answers aliases, type arguments, and duplicate qualified names
    /// with one question instead of three rules.
    /// </remarks>
    private static bool IsReachableFrom(
        IMethodSymbol methodSymbol,
        string invocationTypeName,
        SemanticModel? semanticModel) =>
        GeneratedRegistryAccess.CanReachMember(methodSymbol, semanticModel?.Compilation.Assembly) &&
        GeneratedRegistryAccess.NameBindsToType(invocationTypeName, methodSymbol.ContainingType, semanticModel);

    /// <summary>
    /// Identifies the C# override chain a declaration belongs to.
    /// </summary>
    /// <remarks>
    /// The base-most method in the chain, as its original definition, so a base declaration and an
    /// override of it -- in either order, annotated or not -- answer the same string, while a
    /// <c>new</c> method and an overload answer their own.
    /// <para>
    /// A string rather than the symbol itself, and formatted once per hook per test class rather
    /// than compared symbolically. Keying on the symbol was the alternative and is not open: the key
    /// travels on <see cref="LifecycleMethodDescriptor"/>, which is a value model the incremental
    /// pipeline compares between compilations, and a symbol holds its whole compilation alive.
    /// </para>
    /// <para>
    /// The chain is walked with a visited set for the reason the base walk has one: an override
    /// chain is strictly ascending in any compilation the compiler accepted, and a generator does
    /// not only see those.
    /// </para>
    /// </remarks>
    private static string GetOverrideRootId(IMethodSymbol methodSymbol)
    {
        var root = methodSymbol;
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { root };

        while (root.OverriddenMethod is { } overridden && visited.Add(overridden))
        {
            root = overridden;
        }

        return root.OriginalDefinition.ToDisplayString(_overrideRootFormat);
    }

    private static MethodReturnKind GetReturnKind(IMethodSymbol methodSymbol, KnownReturnTypes knownTypes)
    {
        // The shared classifier reports async void as Void because the analyzers report it through
        // AsyncVoidTestAnalyzer instead. The generator cannot emit an awaitable delegate for it, so
        // it rejects the case here before classifying.
        if (methodSymbol.IsAsync && methodSymbol.ReturnsVoid)
        {
            return MethodReturnKind.Unsupported;
        }

        return knownTypes.Classify(methodSymbol);
    }

    private static bool HasTrailingCancellationToken(IMethodSymbol methodSymbol)
    {
        var parameters = methodSymbol.Parameters;
        return parameters.Length > 0 &&
            parameters[parameters.Length - 1].Type.ToDisplayString() == WellKnownTypeNames.CancellationToken;
    }
}
