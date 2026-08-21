using NextUnit.Generator.Builders;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Emitters;

/// <summary>
/// Emits the <c>LifecycleInfo</c> initializer attached to every descriptor.
/// </summary>
internal static class LifecycleEmitter
{
    private static readonly List<LifecycleMethodDescriptor> _none = new();

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

        EmitMethodArrayProperty(writer, "BeforeTestMethods", Select(lifecycleMethods, isBefore: true, LifecycleScopeConstants.Test));
        EmitMethodArrayProperty(writer, "AfterTestMethods", Select(lifecycleMethods, isBefore: false, LifecycleScopeConstants.Test));
        EmitMethodArrayProperty(writer, "BeforeClassMethods", Select(lifecycleMethods, isBefore: true, LifecycleScopeConstants.Class));
        EmitMethodArrayProperty(writer, "AfterClassMethods", Select(lifecycleMethods, isBefore: false, LifecycleScopeConstants.Class));

        writer.WriteLine("BeforeAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("BeforeSessionMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterSessionMethods = EmptyLifecycleMethods");

        writer.Unindent();
        writer.Write("}");
    }

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
    /// </remarks>
    private static List<LifecycleMethodDescriptor> Select(
        TestLifecycleMethods lifecycleMethods,
        bool isBefore,
        int scope)
    {
        var chosen = new Dictionary<string, LifecycleMethodDescriptor>(StringComparer.Ordinal);

        foreach (var method in lifecycleMethods.BaseToDerived)
        {
            // An unreachable hook is dropped rather than emitted, because NEXTUNIT014 already fails
            // the build and the emitted call would bury it under a CS0122.
            if (!method.IsReachable || !ScopesOf(method, isBefore).Contains(scope))
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

    private static EquatableArray<int> ScopesOf(LifecycleMethodDescriptor method, bool isBefore) =>
        isBefore ? method.BeforeScopes : method.AfterScopes;

    private static void EmitMethodArrayProperty(
        CodeWriter writer,
        string propertyName,
        List<LifecycleMethodDescriptor> methods)
    {
        writer.Write($"{propertyName} = ");
        EmitMethodArray(writer, methods);
        writer.WriteLine(",");
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
