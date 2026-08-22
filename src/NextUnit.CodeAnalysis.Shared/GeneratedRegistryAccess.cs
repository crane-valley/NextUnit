using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// Decides whether <c>NextUnit.Generated.GeneratedTestRegistry</c> can name a symbol.
/// </summary>
/// <remarks>
/// The registry is emitted into the assembly being compiled and is not nested in, or derived from,
/// anything the user wrote. Reachable therefore means reachable from that assembly at large:
/// <c>public</c> is always in reach, <c>internal</c> is in reach because the registry shares the
/// assembly, and a referenced assembly's <c>internal</c> is in reach only through
/// <c>InternalsVisibleTo</c>. <c>private</c>, <c>protected</c>, and <c>private protected</c> are
/// out of reach, and so is anything nested in a scope that is itself out of reach.
/// <para>
/// One implementation serves every emission site on purpose. The retry policy, the data source
/// member, and anything added later all fail the consumer's build the same way when they get this
/// wrong, and two copies of the rule would eventually disagree about which member is safe to emit.
/// </para>
/// </remarks>
internal static class GeneratedRegistryAccess
{
    /// <summary>
    /// Reports whether the generated registry can access <paramref name="member"/> directly.
    /// </summary>
    public static bool CanReachMember(ISymbol member, IAssemblySymbol? compilingAssembly)
    {
        // Nothing to judge against. Returning true keeps the caller on its previous behavior rather
        // than reporting a member as unreachable on the strength of a missing assembly symbol.
        if (compilingAssembly is null)
        {
            return true;
        }

        // A property is read through its getter, so the getter is what has to be in reach. A
        // property declared `public { private get; set; }`, and a set-only property, both look
        // visible on the property symbol and then fail the consumer's build on the read.
        if (member is IPropertySymbol property)
        {
            if (property.GetMethod is null || !IsVisibleToAssembly(property.GetMethod, compilingAssembly))
            {
                return false;
            }
        }
        else if (!IsVisibleToAssembly(member, compilingAssembly))
        {
            return false;
        }

        return member.ContainingType is null || CanReachType(member.ContainingType, compilingAssembly);
    }

    /// <summary>
    /// Reports whether <paramref name="typeExpression"/>, read as a type name in the generated
    /// registry, binds to <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    /// Accessibility is only half of naming a type; the other half is whether the name reaches it
    /// from where the registry is emitted. An assembly reached only through an <c>extern alias</c>
    /// is absent from the global namespace, so the name binds nothing there -- <c>CS0400</c> in a
    /// file the user did not write. Two references that both declare it make the name ambiguous
    /// where the user's own source could have picked one with an alias. A namespace and a type can
    /// collide on it. Source and metadata can share it, and there C# binds to source and only warns.
    /// <para>
    /// So the name is not judged by rules restated here -- it is handed to the binder that will read
    /// it, and the symbol that comes back has to be the intended one. Enumerating the ways a name
    /// can fail to bind was tried first and abandoned: three review rounds each found a rule the
    /// previous one had not modelled, which is what reimplementing name resolution costs. Speculative
    /// binding is position-independent for these names, because a <c>global::</c>-rooted name reads
    /// through no <c>using</c> and no <c>extern alias</c> -- the very reason the registry cannot
    /// write anything else.
    /// </para>
    /// <para>
    /// Requiring the name to be declared by one assembly only was tried on top of this and removed.
    /// It was there because binding discards diagnostics, and a name that source and metadata both
    /// declare binds to source while warning with <c>CS0436</c>. But the registry's file header is a
    /// bare <c>#pragma warning disable</c>, so no warning this name can add reaches the consumer's
    /// build, <c>TreatWarningsAsErrors</c> included -- a suppressed diagnostic is never reported, so
    /// there is nothing left to promote. The check bought nothing and cost the capture this
    /// qualification exists to close: refusing a name falls back to the derived type, which is the
    /// one member a concurrent generator can add. What no pragma suppresses is an error, and the
    /// errors this name can take -- <c>CS0400</c>, <c>CS0433</c> -- already fail the comparison
    /// above, because neither binds to the intended symbol.
    /// </para>
    /// <para>
    /// Emitting an <c>extern alias</c> directive of its own was rejected as the alternative. A
    /// reference can carry several aliases and the registry would have to pick one, the directive
    /// would head every generated file for every consumer, and every emission site would have to
    /// agree on the choice; the callers here have a qualifier that is known to bind and can fall
    /// back to it.
    /// </para>
    /// </remarks>
    public static bool NameBindsToType(string typeExpression, ITypeSymbol type, SemanticModel? semanticModel)
    {
        // Nothing to bind against. Returning true keeps the caller on its previous behavior rather
        // than withholding a name on the strength of a missing semantic model.
        if (semanticModel is null)
        {
            return true;
        }

        var name = SyntaxFactory.ParseTypeName(typeExpression);
        if (name.ContainsDiagnostics)
        {
            return false;
        }

        // Position zero rather than a position near the member: the binder there differs only in the
        // usings and aliases in scope, and this name reads through neither.
        var bound = semanticModel
            .GetSpeculativeSymbolInfo(0, name, SpeculativeBindingOption.BindAsTypeOrNamespace)
            .Symbol;

        return SymbolEqualityComparer.Default.Equals(bound, type);
    }

    /// <summary>
    /// Reports whether the name the registry would write for <paramref name="type"/> spells a type
    /// retired with <c>[Obsolete(..., error: true)]</c>.
    /// </summary>
    /// <remarks>
    /// The one obsolete case a qualifier cannot be written through. The registry's file header is a
    /// bare <c>#pragma warning disable</c>, so every warning a name can add -- <c>CS0618</c> among
    /// them -- is off before any code in the file, <c>TreatWarningsAsErrors</c> included, because a
    /// suppressed diagnostic is never reported and so is never promoted. <c>CS0619</c> is an error,
    /// which no pragma suppresses.
    /// <para>
    /// Handing the question to the binder, as <see cref="NameBindsToType"/> does for name
    /// resolution, was measured on Roslyn 5.6.0 and does not answer it: a speculative semantic model
    /// returns no diagnostics for the very expression that reports <c>CS0619</c> once bound in a
    /// real tree, and the only route left -- adding a syntax tree to the compilation once per data
    /// source, inside a generator -- costs more than the capture it protects. So this one attribute
    /// is read off the symbol instead.
    /// </para>
    /// <para>
    /// The whole name is walked rather than the outermost type alone, because one expression spells
    /// the nesting chain and every type argument under it, and any one of them retired as an error
    /// fails the file. Only the positional flag is read: <c>ObsoleteAttribute.IsError</c> has no
    /// setter, so no named-argument form of it exists, and positional is what Roslyn's own decoder
    /// reads.
    /// </para>
    /// <para>
    /// An unresolved type is deliberately not short-circuited. Answering <c>true</c> for
    /// <c>TypeKind.Error</c> was proposed and declined: <c>true</c> is the value that gives the
    /// qualifier up, so it would let a type the user has yet to write move which member the
    /// generated file reads. <see cref="CanReachType"/> answers the permissive value for that same
    /// kind, and for the same reason -- an error type already carries its own compiler error and
    /// should withhold nothing on top of it. The permissive value here is <c>false</c>, which the
    /// walk returns anyway, since an error type carries no <c>ObsoleteAttribute</c> to find; and
    /// nothing cascades either way, because this reports no diagnostic and only picks a qualifier.
    /// </para>
    /// </remarks>
    public static bool NameSpellsAnErrorObsoleteType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return NameSpellsAnErrorObsoleteType(array.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
        {
            if (IsObsoleteError(current))
            {
                return true;
            }

            foreach (var typeArgument in current.TypeArguments)
            {
                if (NameSpellsAnErrorObsoleteType(typeArgument))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsObsoleteError(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            // The containing type has to be null as well as the namespace right: a nested type
            // reports the namespace of its outermost container, so a `System.Outer.ObsoleteAttribute`
            // answers to the same namespace as the real one and retires nothing.
            //
            // The constructor signature is deliberately not part of the match, though Roslyn's
            // `AttributeDescription` lists `()`, `(string)`, and `(string, bool)` and no others.
            // Measured on 5.6.0: a source assembly declaring its own top-level
            // `System.ObsoleteAttribute` taking `(int, bool)` crashes the compiler that declares it,
            // in `SourceNamespaceSymbol.ForceComplete`, casting the first argument to string -- so
            // the name is what Roslyn matched on there, not the signature. Requiring the signature
            // would risk reading a homonym Roslyn does treat as obsolete as harmless, and this
            // predicate is asymmetric: a false positive costs the qualifier, which is the fallback
            // an unbindable name already takes, and a false negative costs the consumer a build.
            if (attribute.AttributeClass is
                {
                    Name: "ObsoleteAttribute",
                    ContainingType: null,
                    ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
                } &&
                attribute.ConstructorArguments.Length > 1 &&
                attribute.ConstructorArguments[1].Value is true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether the generated registry can name <paramref name="type"/>.
    /// </summary>
    /// <remarks>
    /// The containing type chain is walked because a <c>public</c> type nested in a <c>private</c>
    /// one is unreachable all the same, and every type argument with it, because a reachable
    /// <c>Fixture&lt;T&gt;</c> still cannot be named when <c>T</c> cannot.
    /// </remarks>
    public static bool CanReachType(ITypeSymbol type, IAssemblySymbol? compilingAssembly)
    {
        if (compilingAssembly is null)
        {
            return true;
        }

        // An unresolved type carries no accessibility to judge and already has its own compiler
        // error. Reporting on top of it would add a visibility complaint the user cannot act on and
        // bury the error that actually needs fixing.
        if (type.TypeKind == TypeKind.Error)
        {
            return true;
        }

        // An array is named through its element type, and a type parameter is substituted at the use
        // site, so neither has a visibility of its own to check.
        if (type is IArrayTypeSymbol array)
        {
            return CanReachType(array.ElementType, compilingAssembly);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
        {
            // A file-local type reports internal accessibility but has a mangled metadata name and
            // can only be named inside its own source file, so the registry cannot reach it however
            // visible it looks.
            if (current.IsFileLocal)
            {
                return false;
            }

            if (!IsVisibleToAssembly(current, compilingAssembly))
            {
                return false;
            }

            foreach (var typeArgument in current.TypeArguments)
            {
                if (!CanReachType(typeArgument, compilingAssembly))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Reports whether a symbol's own declared accessibility admits the compiling assembly.
    /// </summary>
    public static bool IsVisibleToAssembly(ISymbol symbol, IAssemblySymbol compilingAssembly)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return true;

            case Accessibility.Internal:
            case Accessibility.ProtectedOrInternal:
                return SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, compilingAssembly) ||
                    symbol.ContainingAssembly?.GivesAccessTo(compilingAssembly) == true;

            default:
                // Private, protected, and private protected are all out of reach from a type that is
                // neither the declaring type nor derived from it.
                return false;
        }
    }

}
