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

        EmitMethodArrayProperty(writer, "BeforeTestMethods", lifecycleMethods, isBefore: true, LifecycleScopeConstants.Test);
        EmitMethodArrayProperty(writer, "AfterTestMethods", lifecycleMethods, isBefore: false, LifecycleScopeConstants.Test);
        EmitMethodArrayProperty(writer, "BeforeClassMethods", lifecycleMethods, isBefore: true, LifecycleScopeConstants.Class);
        EmitMethodArrayProperty(writer, "AfterClassMethods", lifecycleMethods, isBefore: false, LifecycleScopeConstants.Class);

        writer.WriteLine("BeforeAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterAssemblyMethods = EmptyLifecycleMethods,");
        writer.WriteLine("BeforeSessionMethods = EmptyLifecycleMethods,");
        writer.WriteLine("AfterSessionMethods = EmptyLifecycleMethods");

        writer.Unindent();
        writer.Write("}");
    }

    private static void EmitMethodArrayProperty(
        CodeWriter writer,
        string propertyName,
        TestLifecycleMethods lifecycleMethods,
        bool isBefore,
        int scope)
    {
        // An unreachable hook is dropped rather than emitted, because NEXTUNIT015 already fails the
        // build and the emitted call would bury that report under a CS0122.
        var methods = LifecycleSelection.Select(lifecycleMethods, isBefore, scope)
            .Where(static method => method.IsReachable)
            .ToList();

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
