using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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
            if (method.Name == memberName &&
                IsVisibleToDerivedType(method, method.IsAssembly || method.IsFamilyAndAssembly, sourceType))
            {
                candidates.Add(method);
            }
        }

        foreach (var property in sourceType.GetProperties(StaticMemberLookup))
        {
            // A property is read through its getter, so the getter's accessibility is the one that
            // decides whether the derived class can see it.
            var getter = property.GetMethod;
            if (property.Name == memberName &&
                getter is not null &&
                IsVisibleToDerivedType(property, getter.IsAssembly || getter.IsFamilyAndAssembly, sourceType))
            {
                candidates.Add(property);
            }
        }

        foreach (var field in sourceType.GetFields(StaticMemberLookup))
        {
            if (field.Name == memberName &&
                IsVisibleToDerivedType(field, field.IsAssembly || field.IsFamilyAndAssembly, sourceType))
            {
                candidates.Add(field);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Reports whether C# member lookup from <paramref name="sourceType"/> can see a member
    /// declared on one of its base types.
    /// </summary>
    /// <remarks>
    /// Mirrors the same rule in <c>DataSourceMemberResolver</c>. Across an assembly boundary,
    /// <c>internal</c> and <c>private protected</c> are out of scope for the derived class unless
    /// the declaring assembly grants <c>InternalsVisibleTo</c>, so neither hides an accessible
    /// ancestor of the same name -- and treating one as a candidate here would read a member the
    /// name does not refer to. Members declared on <paramref name="sourceType"/> itself are left
    /// alone: that is the member the user named, and the fallback has always read whatever reaches
    /// it there.
    /// </remarks>
    private static bool IsVisibleToDerivedType(MemberInfo member, bool needsAssemblyAccess, Type sourceType)
    {
        var declaringType = member.DeclaringType;
        if (declaringType is null || declaringType == sourceType || !needsAssemblyAccess)
        {
            return true;
        }

        return declaringType.Assembly == sourceType.Assembly ||
            GivesInternalAccessTo(declaringType.Assembly, sourceType.Assembly);
    }

    private static bool GivesInternalAccessTo(Assembly declaring, Assembly consuming)
    {
        var consumingName = consuming.GetName().Name;

        foreach (var visibleTo in declaring.GetCustomAttributes<InternalsVisibleToAttribute>())
        {
            // The attribute value carries an optional PublicKey after a comma; only the simple
            // assembly name decides whether the grant names this assembly.
            var granted = visibleTo.AssemblyName;
            var comma = granted.IndexOf(',');
            if (comma >= 0)
            {
                granted = granted.Substring(0, comma);
            }

            if (string.Equals(granted.Trim(), consumingName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether C# would bind this overload for a call that supplies no arguments.
    /// </summary>
    /// <remarks>
    /// Optional parameters and a trailing <c>params</c> array are both filled in by the compiler,
    /// so <c>Rows(int count = 1)</c> answers <c>Rows()</c> and reduces away any base <c>Rows()</c>.
    /// Reflection cannot supply the omitted arguments, so such an overload is a blocker here rather
    /// than something to invoke.
    /// </remarks>
    private static bool IsApplicableWithoutArguments(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            if (!parameter.IsOptional && !parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false))
            {
                return false;
            }
        }

        return true;
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
    private static MemberInfo? SelectMember(Type sourceType, List<MemberInfo> candidates)
    {
        if (candidates.Count == 0)
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

                // An overload C# would still bind for a no-argument call reduces away every base
                // overload of that name, so falling through to one would read data this name does
                // not refer to. Reflection cannot fill in the omitted arguments, so the search ends
                // here with nothing and the caller reports the source as missing.
                if (!method.IsGenericMethodDefinition && IsApplicableWithoutArguments(method))
                {
                    return null;
                }

                // An overload that genuinely requires arguments hides a base member that is not a
                // method, but leaves a base overload of another shape alone -- so the walk carries
                // on.
                sawMethod = true;
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
