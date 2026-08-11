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
/// </remarks>
internal static class GeneratedRegistryAccess
{
    /// <summary>
    /// Reports whether the generated registry can access <paramref name="member"/> directly.
    /// </summary>
    /// <remarks>
    /// The containing type chain is walked as well as the member: a <c>public</c> member of a
    /// <c>private</c> nested type is unreachable all the same, and emitting access to it fails the
    /// consumer's build with <c>CS0122</c> inside generated code.
    /// </remarks>
    public static bool CanReachMember(ISymbol member, IAssemblySymbol? compilingAssembly)
    {
        // Nothing to judge against. Returning true keeps the caller on its previous behavior rather
        // than reporting a member as unreachable on the strength of a missing assembly symbol.
        if (compilingAssembly is null)
        {
            return true;
        }

        if (!IsVisibleToAssembly(member, compilingAssembly))
        {
            return false;
        }

        for (INamedTypeSymbol? type = member.ContainingType; type is not null; type = type.ContainingType)
        {
            // A file-local type reports internal accessibility but has a mangled metadata name and
            // can only be named inside its own source file, so the registry cannot reach it however
            // visible it looks.
            if (type.IsFileLocal)
            {
                return false;
            }

            if (!IsVisibleToAssembly(type, compilingAssembly))
            {
                return false;
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
