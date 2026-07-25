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
    /// methods are collected once into the registry's static properties, so the corresponding
    /// arrays here are always empty.
    /// </remarks>
    public static void EmitLifecycleInfo(
        CodeWriter writer,
        string typeName,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        var beforeTest = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var afterTest = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var beforeClass = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Class)).ToList();
        var afterClass = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Class)).ToList();

        writer.WriteLine("new global::NextUnit.Internal.LifecycleInfo");
        writer.WriteLine("{");
        writer.Indent();

        EmitMethodArrayProperty(writer, "BeforeTestMethods", typeName, beforeTest);
        EmitMethodArrayProperty(writer, "AfterTestMethods", typeName, afterTest);
        EmitMethodArrayProperty(writer, "BeforeClassMethods", typeName, beforeClass);
        EmitMethodArrayProperty(writer, "AfterClassMethods", typeName, afterClass);

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
        string typeName,
        List<LifecycleMethodDescriptor> methods)
    {
        writer.Write($"{propertyName} = ");
        EmitMethodArray(writer, typeName, methods);
        writer.WriteLine(",");
    }

    private static void EmitMethodArray(
        CodeWriter writer,
        string typeName,
        List<LifecycleMethodDescriptor> methods)
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
                $"{CodeBuilder.BuildLifecycleMethodDelegate(typeName, method.MethodName, method.IsStatic, method.ReturnKind, method.AcceptsCancellationToken)},");
        }

        writer.Unindent();
        writer.Write("}");
    }
}
