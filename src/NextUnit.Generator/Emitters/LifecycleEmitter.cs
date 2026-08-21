using NextUnit.Generator.Builders;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Emitters;

/// <summary>
/// Emits the <c>LifecycleInfo</c> initializer attached to every descriptor.
/// </summary>
internal static class LifecycleEmitter
{
    /// <summary>
    /// Emits a lifecycle info initializer, continuing the line the caller has open and leaving the
    /// closing brace unterminated so the caller can append its own separator.
    /// </summary>
    /// <remarks>
    /// Only the Test and Class scopes are emitted per descriptor. Assembly and Session scoped
    /// methods are collected once into the registry static properties, so the corresponding arrays
    /// here are always empty -- and that is also why those two scopes are not inherited: they run
    /// once for the whole run, and running them once per derived class instead is not the same
    /// thing.
    /// </remarks>
    public static void EmitLifecycleInfo(CodeWriter writer, TestLifecycleMethods lifecycleMethods)
    {
        writer.WriteLine("new global::NextUnit.Internal.LifecycleInfo");
        writer.WriteLine("{");
        writer.Indent();

        var testBefore = SelectEmitted(lifecycleMethods, isBefore: true, LifecycleScopeConstants.Test);
        var testAfter = SelectEmitted(lifecycleMethods, isBefore: false, LifecycleScopeConstants.Test);
        var classBefore = SelectEmitted(lifecycleMethods, isBefore: true, LifecycleScopeConstants.Class);
        var classAfter = SelectEmitted(lifecycleMethods, isBefore: false, LifecycleScopeConstants.Class);

        EmitMethodArrayProperty(writer, "BeforeTestMethods", testBefore);
        EmitMethodArrayProperty(writer, "AfterTestMethods", testAfter);
        EmitMethodArrayProperty(writer, "BeforeClassMethods", classBefore);
        EmitMethodArrayProperty(writer, "AfterClassMethods", classAfter);
        EmitLevelsProperty(writer, "TestLevels", lifecycleMethods.LevelOrder, testBefore, testAfter);
        EmitLevelsProperty(writer, "ClassLevels", lifecycleMethods.LevelOrder, classBefore, classAfter);

        writer.WriteLine("BeforeAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("BeforeSessionMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterSessionMethods = EmptyLifecycleMethods");

        writer.Unindent();
        writer.Write("}");
    }

    /// <summary>
    /// The hooks of one direction and one scope that actually reach the registry.
    /// </summary>
    /// <remarks>
    /// An unreachable hook is dropped rather than emitted, because NEXTUNIT015 already fails the
    /// build and the emitted call would bury that report under a CS0122.
    /// </remarks>
    private static List<LifecycleMethodDescriptor> SelectEmitted(
        TestLifecycleMethods lifecycleMethods,
        bool isBefore,
        int scope) =>
        LifecycleSelection.Select(lifecycleMethods, isBefore, scope)
            .Where(static method => method.IsReachable)
            .ToList();

    private static void EmitMethodArrayProperty(
        CodeWriter writer,
        string propertyName,
        List<LifecycleMethodDescriptor> methods)
    {
        writer.Write($"{propertyName} = ");
        EmitMethodArray(writer, methods);
        writer.WriteLine(",");
    }

    /// <summary>
    /// Emits how a scope's hooks divide into base-chain levels, so teardown can unwind the levels the
    /// run entered instead of all of them.
    /// </summary>
    /// <remarks>
    /// The levels are the ones the emitted selections actually contain, not every class in the base
    /// chain: a hook removed because it belongs to another scope, because an override collapsed it, or
    /// because the registry cannot reach it must not leave a level behind that no hook belongs to.
    /// <c>LevelOrder</c> supplies only the base-to-derived ordering.
    /// <para>
    /// Nothing is emitted for a scope with one level or none. An empty list already means one level
    /// holding every hook, so a test class with no annotated base class -- and every descriptor
    /// written against the pre-3.0.0 shape -- keeps its generated output byte for byte.
    /// </para>
    /// </remarks>
    private static void EmitLevelsProperty(
        CodeWriter writer,
        string propertyName,
        List<string> levelOrder,
        List<LifecycleMethodDescriptor> beforeMethods,
        List<LifecycleMethodDescriptor> afterMethods)
    {
        var levels = new List<(int Before, int After)>();
        foreach (var level in levelOrder)
        {
            var before = CountIn(beforeMethods, level);
            var after = CountIn(afterMethods, level);
            if (before > 0 || after > 0)
            {
                levels.Add((before, after));
            }
        }

        if (levels.Count < 2)
        {
            return;
        }

        writer.WriteLine($"{propertyName} = new global::NextUnit.Internal.LifecycleLevel[]");
        writer.WriteLine("{");
        writer.Indent();

        foreach (var (before, after) in levels)
        {
            writer.WriteLine(
                $"new global::NextUnit.Internal.LifecycleLevel {{ BeforeCount = {before}, AfterCount = {after} }},");
        }

        writer.Unindent();
        writer.WriteLine("},");
    }

    private static int CountIn(List<LifecycleMethodDescriptor> methods, string invocationTypeName)
    {
        var count = 0;
        foreach (var method in methods)
        {
            if (string.Equals(method.InvocationTypeName, invocationTypeName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static void EmitMethodArray(CodeWriter writer, List<LifecycleMethodDescriptor> methods)
    {
        if (methods.Count == 0)
        {
            writer.Write("EmptyLifecycleMethods");
            return;
        }

        writer.WriteLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
        writer.WriteLine("{");
        writer.Indent();

        foreach (var method in methods)
        {
            writer.WriteLine(
                $"{CodeBuilder.BuildLifecycleMethodDelegate(method.InvocationTypeName, method.MethodName, method.IsStatic, method.ReturnKind, method.AcceptsCancellationToken)},");
        }

        writer.Unindent();
        writer.Write("}");
    }
}
