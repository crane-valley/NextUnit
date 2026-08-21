using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Chooses which of a test's hooks run for one direction and one scope, and in what order.
/// </summary>
/// <remarks>
/// Shared by the emitter and the validator on purpose. The validator reports a hook the registry
/// cannot call, and reporting one the emitter would never have emitted turns working code into a
/// build failure that names a hook the user could not have run anyway. One selection means the two
/// cannot disagree about which hooks those are.
/// </remarks>
internal static class LifecycleSelection
{
    private static readonly List<LifecycleMethodDescriptor> _none = new();

    /// <summary>
    /// The hooks of one direction and one scope, in execution order, with each override chain
    /// represented exactly once.
    /// </summary>
    /// <remarks>
    /// The declaration that survives is the base-most one, and it keeps that class position, so
    /// re-declaring <c>[Before]</c> on an override does not move the hook: the emitted call casts to
    /// the base type and dispatches virtually, which runs the most derived body from the base slot.
    /// Selection runs per scope because a derived override may re-declare a different scope than the
    /// base did, and collapsing the two declarations before the scope is known would lose one.
    /// <para>
    /// Identity is the override chain, never the method name. A <c>new</c> method is a different
    /// method, so it is a second hook rather than a replacement, and the two are told apart here
    /// exactly as C# tells them apart.
    /// </para>
    /// <para>
    /// Reachability is deliberately not filtered here. A hook the registry cannot call still wins
    /// its slot, because the alternative is to silently promote the base declaration it supersedes;
    /// the caller drops it and <c>NEXTUNIT015</c> reports it instead.
    /// </para>
    /// </remarks>
    public static List<LifecycleMethodDescriptor> Select(
        TestLifecycleMethods lifecycleMethods,
        bool isBefore,
        int scope)
    {
        var chosen = new Dictionary<string, LifecycleMethodDescriptor>(StringComparer.Ordinal);

        foreach (var method in lifecycleMethods.BaseToDerived)
        {
            if (!ScopesOf(method, isBefore).Contains(scope))
            {
                continue;
            }

            if (!chosen.ContainsKey(method.OverrideRootId))
            {
                chosen.Add(method.OverrideRootId, method);
            }
        }

        if (chosen.Count == 0)
        {
            return _none;
        }

        var ordered = isBefore ? lifecycleMethods.BaseToDerived : lifecycleMethods.DerivedToBase;
        var selected = new List<LifecycleMethodDescriptor>(chosen.Count);

        foreach (var method in ordered)
        {
            if (chosen.TryGetValue(method.OverrideRootId, out var kept) && ReferenceEquals(kept, method))
            {
                selected.Add(method);
            }
        }

        return selected;
    }

    /// <summary>
    /// The scopes emitted per test descriptor. Assembly and Session are collected once into the
    /// registry static properties instead, so a hook declaring only those is not selected here.
    /// </summary>
    public static readonly int[] PerDescriptorScopes =
        [LifecycleScopeConstants.Test, LifecycleScopeConstants.Class];

    private static EquatableArray<int> ScopesOf(LifecycleMethodDescriptor method, bool isBefore) =>
        isBefore ? method.BeforeScopes : method.AfterScopes;
}
