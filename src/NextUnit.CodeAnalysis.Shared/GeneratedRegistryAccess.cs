using System.Text;
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
    /// Binding alone is not quite enough, because it discards diagnostics. A name that source and
    /// metadata both declare binds to source and warns with <c>CS0436</c>, and a project promoting
    /// warnings to errors would fail on the generated file even though the user's own file may carry
    /// a <c>#pragma</c> for it. So a name any other assembly also declares is refused as well: the
    /// qualifier is only worth having where it costs the consumer's build nothing.
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

        return SymbolEqualityComparer.Default.Equals(bound, type) &&
            IsDeclaredOnce(type, semanticModel.Compilation);
    }

    /// <summary>
    /// Reports whether the type, and everything its name is composed from, is declared by one
    /// assembly only.
    /// </summary>
    /// <remarks>
    /// The question binding cannot answer: which diagnostic the use would carry. A name declared
    /// twice still binds -- to source, over metadata -- and warns with <c>CS0436</c>, so it is
    /// refused here rather than emitted into a build that may promote that warning. The nesting chain
    /// is checked link by link because the shadowed declaration can be an outer type, and the type
    /// arguments because they are written out too.
    /// </remarks>
    private static bool IsDeclaredOnce(ITypeSymbol type, Compilation compilation)
    {
        if (type is IArrayTypeSymbol array)
        {
            return IsDeclaredOnce(array.ElementType, compilation);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return true;
        }

        for (INamedTypeSymbol? current = namedType.OriginalDefinition; current is not null; current = current.ContainingType)
        {
            if (compilation.GetTypesByMetadataName(GetFullMetadataName(current)).Length > 1)
            {
                return false;
            }
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            if (!IsDeclaredOnce(typeArgument, compilation))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the metadata name a type is looked up by: namespace-qualified, nesting joined with
    /// <c>+</c>, and generic arity already carried by each part's own metadata name.
    /// </summary>
    private static string GetFullMetadataName(INamedTypeSymbol type)
    {
        var builder = new StringBuilder(type.MetadataName);

        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            builder.Insert(0, '+').Insert(0, containing.MetadataName);
        }

        if (type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace)
        {
            builder.Insert(0, '.').Insert(0, containingNamespace.ToDisplayString());
        }

        return builder.ToString();
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
