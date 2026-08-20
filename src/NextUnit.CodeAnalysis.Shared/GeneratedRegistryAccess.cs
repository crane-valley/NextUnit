using Microsoft.CodeAnalysis;

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
    /// Reports whether the generated registry can write <paramref name="type"/> as a
    /// <c>global::</c>-qualified name.
    /// </summary>
    /// <remarks>
    /// Accessibility is only half of naming a type. The registry is emitted with no
    /// <c>extern alias</c> directives, so a reference whose aliases exclude the global one puts
    /// nothing in the global namespace the emitted text reads from: <c>global::N.T</c> there fails
    /// to bind, or binds a homonym some other reference did put in the global namespace. Asked of
    /// the reference rather than of the symbol, because that is the only place the aliases live.
    /// <para>
    /// Emitting an <c>extern alias</c> directive of its own was rejected as the answer. A reference
    /// can carry several aliases and the registry would have to pick one, the directive would head
    /// every generated file for every consumer, and every emission site would have to agree on the
    /// choice; the callers here have a qualifier that is known to bind and can fall back to it.
    /// </para>
    /// </remarks>
    public static bool CanNameTypeWithoutAlias(ITypeSymbol type, Compilation? compilation)
    {
        if (compilation is null)
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return CanNameTypeWithoutAlias(array.ElementType, compilation);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        if (!IsInGlobalAlias(namedType.ContainingAssembly, compilation))
        {
            return false;
        }

        // The containing type chain is walked for its type arguments and not for its assembly, which
        // is always the assembly of the type it contains: Outer<A::Row>.Inner carries the argument
        // that decides the answer one level up.
        for (INamedTypeSymbol? current = namedType; current is not null; current = current.ContainingType)
        {
            foreach (var typeArgument in current.TypeArguments)
            {
                if (!CanNameTypeWithoutAlias(typeArgument, compilation))
                {
                    return false;
                }
            }
        }

        return true;
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

    /// <summary>
    /// Reports whether an assembly's contents reach the global namespace.
    /// </summary>
    private static bool IsInGlobalAlias(IAssemblySymbol? assembly, Compilation compilation)
    {
        // Source is always in the global namespace, and a symbol with no assembly is an error type
        // that carries a compiler error of its own; neither is judged on a reference it has none of.
        if (assembly is null || SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly))
        {
            return true;
        }

        // A reference the compilation cannot hand back leaves nothing to read the aliases from.
        // Answering yes keeps the caller on the name it would have written anyway rather than
        // withholding one on the strength of a missing reference.
        if (compilation.GetMetadataReference(assembly) is not { } reference)
        {
            return true;
        }

        var aliases = reference.Properties.Aliases;
        return aliases.IsEmpty || aliases.Contains(MetadataReferenceProperties.GlobalAlias);
    }
}
