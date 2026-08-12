using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Reads a static data source member reflectively, resolving the name the way C# does.
/// </summary>
/// <remarks>
/// The reflection fallback for a member the source generator could not bind. One implementation
/// serves <c>[TestData]</c> and <c>[ValuesFromMember]</c> alike, mirroring
/// <c>DataSourceMemberResolver</c> on the compile-time side: two copies of the rule would eventually
/// disagree about which member a name means, and the two sides disagreeing is how a suite ends up
/// running data nobody asked for.
/// </remarks>
internal static class DataSourceMemberLookup
{
    /// <remarks>
    /// <see cref="BindingFlags.FlattenHierarchy"/> is what reaches a member declared on a base test
    /// class: without it the lookup stops at the named type, so a source C# resolves as
    /// <c>Derived.Rows</c> was reported as missing. It also drops a base declaration that a derived
    /// type shadows, and never returns a base type's <c>private</c> members -- which C# member
    /// lookup does not see from a derived type either, so the compile-time resolver skips them too.
    /// <para>
    /// <see cref="BindingFlags.Instance"/> is here for hiding, not for reading. A derived instance
    /// member hides the base static one it repeats, so leaving it out of the candidates would let
    /// this read a base member the name no longer refers to. Nothing non-static is ever returned.
    /// </para>
    /// </remarks>
    private const BindingFlags StaticMemberLookup =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance |
        BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Reads the value of the static member <paramref name="memberName"/> names on
    /// <paramref name="sourceType"/> or one of its base types.
    /// </summary>
    /// <returns><c>true</c> when a member was found and read; <c>false</c> when the name binds to nothing.</returns>
    public static bool TryReadStaticMember(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type sourceType,
        string memberName,
        out object? value)
    {
        switch (SelectMember(sourceType, CollectCandidates(sourceType, memberName)))
        {
            case MethodInfo method:
                value = method.Invoke(null, null);
                return true;

            case PropertyInfo property:
                value = property.GetValue(null);
                return true;

            case FieldInfo field:
                value = field.GetValue(null);
                return true;

            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Gathers every method, property, and field of that name, flattened across the base chain.
    /// </summary>
    /// <remarks>
    /// One call per member kind rather than a single <c>GetMember</c> with a
    /// <see cref="MemberTypes"/> mask. The trimmer does not read that mask: it treats
    /// <c>GetMember</c> as able to return any kind and demands the annotations for constructors,
    /// events, and nested types as well, which is <c>IL2070</c> and, in the Native AOT smoke test,
    /// a failed build. The per-kind calls ask for exactly what the annotation on
    /// <see cref="TryReadStaticMember"/> already grants.
    /// <para>
    /// Events and nested types are therefore not among the candidates, so neither blocks a
    /// same-named source further up the chain the way C# hiding would. Both are stopped earlier
    /// instead: the compile-time walk sees them, binds nothing, and <c>NU0003</c> fails the build
    /// before this runs. Widening the annotations to cover them would make every test class and
    /// data source type keep its events and nested types under trimming, which is a real cost for
    /// a case that cannot reach here.
    /// </para>
    /// </remarks>
    private static List<MemberInfo> CollectCandidates(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type sourceType,
        string memberName)
    {
        var candidates = new List<MemberInfo>();

        foreach (var method in sourceType.GetMethods(StaticMemberLookup))
        {
            if (method.Name == memberName)
            {
                candidates.Add(method);
            }
        }

        foreach (var property in sourceType.GetProperties(StaticMemberLookup))
        {
            if (property.Name == memberName)
            {
                candidates.Add(property);
            }
        }

        foreach (var field in sourceType.GetFields(StaticMemberLookup))
        {
            if (field.Name == memberName)
            {
                candidates.Add(field);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Picks the member the name means: the nearest declaring level wins, or nothing does.
    /// </summary>
    /// <remarks>
    /// The runtime half of the contract in <c>DataSourceMemberResolver.GetCandidateMembers</c>.
    /// Candidates arrive flattened across the base chain, so they are walked by declaring type:
    /// whichever level first declares the name is the only level that can answer it, and a level
    /// that declares the name without offering a readable static member ends the search rather than
    /// deferring to a farther one. Searching kind by kind instead -- every property, then every
    /// field, then every method -- would read a base member for a name a nearer type has taken
    /// over, which is a test running against data the user never pointed at.
    /// <para>
    /// The whole level is scanned before it is rejected, so an overload that cannot be invoked here
    /// never hides a sibling that can: a type declaring both <c>Rows(int)</c> and <c>Rows()</c>
    /// resolves to <c>Rows()</c> whatever order reflection happens to return them in.
    /// </para>
    /// <para>
    /// Only <paramref name="sourceType"/>'s own base chain is walked, by identity alone. Nothing is
    /// reflected over a base type here, so the annotation on the entry point stays sufficient.
    /// </para>
    /// </remarks>
    private static MemberInfo? SelectMember(Type sourceType, List<MemberInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        for (Type? level = sourceType; level is not null; level = level.BaseType)
        {
            var declaresName = false;
            MemberInfo? readable = null;

            foreach (var candidate in candidates)
            {
                if (candidate.DeclaringType != level)
                {
                    continue;
                }

                declaresName = true;

                if (candidate is MethodInfo method)
                {
                    // Arity is part of the test for the same reason it is in the compile-time
                    // resolver: the call supplies no type argument, so a generic overload could not
                    // be invoked. An overload taking arguments is simply not this member, and the
                    // scan carries on through the rest of the level to find one that is.
                    if (method.IsStatic &&
                        method.GetParameters().Length == 0 &&
                        !method.IsGenericMethodDefinition)
                    {
                        return method;
                    }
                }
                else if (readable is null && IsStatic(candidate))
                {
                    readable = candidate;
                }
            }

            if (readable is not null)
            {
                return readable;
            }

            // The level declares the name, so it is the one the compiler binds. Nothing on it can
            // be read, and falling through to a farther level would answer with a member this name
            // does not refer to.
            if (declaresName)
            {
                return null;
            }
        }

        return null;
    }

    /// <remarks>
    /// A property is read through its getter, so the getter is what has to be static -- and a
    /// property with no getter cannot be read at all. Everything else that reaches here is a kind
    /// this never reads, so it reports false and ends the search as the hiding declaration it is.
    /// </remarks>
    private static bool IsStatic(MemberInfo member) => member switch
    {
        PropertyInfo property => property.GetMethod?.IsStatic == true,
        FieldInfo field => field.IsStatic,
        _ => false
    };
}
