namespace NextUnit.Generator.Models;

/// <summary>
/// The hooks of one test, in both directions the emitter needs.
/// </summary>
/// <remarks>
/// Only the class levels are reversed for <c>[After]</c>, never the hooks inside one class. That
/// is what lets <c>[Before]</c> run base to derived and <c>[After]</c> derived to base while the
/// order of several hooks declared in one class stays what it was before inheritance existed.
/// </remarks>
internal sealed class TestLifecycleMethods
{
    private TestLifecycleMethods(
        List<LifecycleMethodDescriptor> baseToDerived,
        List<LifecycleMethodDescriptor> derivedToBase)
    {
        BaseToDerived = baseToDerived;
        DerivedToBase = derivedToBase;
    }

    public List<LifecycleMethodDescriptor> BaseToDerived { get; }

    public List<LifecycleMethodDescriptor> DerivedToBase { get; }

    public static TestLifecycleMethods Create(
        EquatableArray<LifecycleMethodDescriptor> inherited,
        List<LifecycleMethodDescriptor> declared)
    {
        var levels = SplitIntoLevels(inherited);
        levels.Add(declared);

        var baseToDerived = new List<LifecycleMethodDescriptor>();
        foreach (var level in levels)
        {
            baseToDerived.AddRange(level);
        }

        var derivedToBase = new List<LifecycleMethodDescriptor>();
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            derivedToBase.AddRange(levels[i]);
        }

        return new TestLifecycleMethods(baseToDerived, derivedToBase);
    }

    /// <summary>
    /// Splits the inherited hooks into one list per declaring class, base-most first.
    /// </summary>
    /// <remarks>
    /// The array arrives grouped by declaring class already, so a level is a contiguous run
    /// sharing an invocation type. Splitting on that rather than carrying a level index keeps the
    /// index out of the descriptor, where it would be a per-test value on a value model that is
    /// otherwise about the declaration alone.
    /// </remarks>
    private static List<List<LifecycleMethodDescriptor>> SplitIntoLevels(
        EquatableArray<LifecycleMethodDescriptor> inherited)
    {
        var levels = new List<List<LifecycleMethodDescriptor>>();

        for (var i = 0; i < inherited.Length; i++)
        {
            var method = inherited[i];
            if (levels.Count == 0 ||
                !string.Equals(levels[levels.Count - 1][0].InvocationTypeName, method.InvocationTypeName, StringComparison.Ordinal))
            {
                levels.Add(new List<LifecycleMethodDescriptor>());
            }

            levels[levels.Count - 1].Add(method);
        }

        return levels;
    }
}
