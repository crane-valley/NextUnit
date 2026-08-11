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

    private const MemberTypes DataSourceMemberKinds =
        MemberTypes.Method | MemberTypes.Property | MemberTypes.Field;

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
        var candidates = sourceType.GetMember(memberName, DataSourceMemberKinds, StaticMemberLookup);

        switch (SelectMember(sourceType, candidates))
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
    /// Picks the member the name means, applying the hiding rules C# applies.
    /// </summary>
    /// <remarks>
    /// The candidates arrive flattened, so they have to be walked by declaring type rather than by
    /// member kind. Searching kind by kind instead -- every property, then every field, then every
    /// method -- reads a base property for a name a derived method has taken over, which is a test
    /// running silently against data the user did not point at. So: a member that is not a method
    /// wins its level and ends the walk, unless a nearer method already claimed the name, in which
    /// case the name is a method group and binds to nothing here. Methods carry on up the chain,
    /// which is what lets a base <c>Rows()</c> answer a name a derived <c>Rows(CancellationToken)</c>
    /// also declares -- the overload C# picks for a call supplying no arguments.
    /// <para>
    /// A winner that turns out to be an instance member ends the search with nothing rather than
    /// falling through to the base declaration it hides. That is the same verdict the compile-time
    /// resolver reaches, where a derived instance <c>Rows()</c> makes the name unusable as a static
    /// reference; reading the base member instead would run the test against data the name does not
    /// refer to.
    /// </para>
    /// <para>
    /// Only <paramref name="sourceType"/>'s own base chain is walked, by identity alone. Nothing is
    /// reflected over a base type here, so the annotation on the entry point stays sufficient.
    /// </para>
    /// </remarks>
    private static MemberInfo? SelectMember(Type sourceType, MemberInfo[] candidates)
    {
        if (candidates.Length == 0)
        {
            return null;
        }

        var sawMethod = false;

        for (Type? level = sourceType; level is not null; level = level.BaseType)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.DeclaringType != level)
                {
                    continue;
                }

                if (candidate is not MethodInfo method)
                {
                    return sawMethod || !IsStatic(candidate) ? null : candidate;
                }

                // Arity is part of the test for the same reason it is in the compile-time resolver:
                // the call supplies no type argument, so a generic overload could not be invoked.
                if (method.GetParameters().Length == 0 && !method.IsGenericMethodDefinition)
                {
                    return method.IsStatic ? method : null;
                }

                // An overload that takes arguments hides a base member that is not a method, but
                // leaves a base overload of another signature alone -- so the walk carries on.
                sawMethod = true;
            }
        }

        return null;
    }

    private static bool IsStatic(MemberInfo member) => member switch
    {
        PropertyInfo property => property.GetMethod?.IsStatic == true,
        FieldInfo field => field.IsStatic,
        _ => false
    };
}
