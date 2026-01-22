using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Formatters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Builders;

/// <summary>
/// Builds code literals and delegates for the generated test registry.
/// </summary>
internal static class CodeBuilder
{
    /// <summary>
    /// Builds a test method delegate.
    /// </summary>
    public static string BuildTestMethodDelegate(string typeName, string methodName, bool isStatic)
    {
        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeTestMethodAsync({typeName}.{methodName}, ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeTestMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    /// <summary>
    /// Builds a parameterized test method delegate.
    /// </summary>
    public static string BuildParameterizedTestMethodDelegate(
        string typeName,
        string methodName,
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<TypedConstant> arguments,
        bool isStatic)
    {
        var argsBuilder = new StringBuilder();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                argsBuilder.Append(", ");
            }

            var arg = arguments[i];
            var param = i < parameters.Length ? parameters[i] : null;

            argsBuilder.Append(ArgumentFormatter.FormatArgumentValue(arg, param?.Type));
        }

        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeTestMethodAsync(() => {typeName}.{methodName}({argsBuilder}), ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeTestMethodAsync(() => typedInstance.{methodName}({argsBuilder}), ct).ConfigureAwait(false); }}";
    }

    /// <summary>
    /// Builds a lifecycle method delegate.
    /// </summary>
    public static string BuildLifecycleMethodDelegate(string typeName, string methodName, bool isStatic)
    {
        return isStatic
            ? $"static async (instance, ct) => {{ await InvokeLifecycleMethodAsync({typeName}.{methodName}, ct).ConfigureAwait(false); }}"
            : $"static async (instance, ct) => {{ var typedInstance = ({typeName})instance; await InvokeLifecycleMethodAsync(typedInstance.{methodName}, ct).ConfigureAwait(false); }}";
    }

    /// <summary>
    /// Builds a lifecycle info literal.
    /// </summary>
    public static string BuildLifecycleInfoLiteral(string typeName, List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        var beforeTest = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var afterTest = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var beforeClass = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Class)).ToList();
        var afterClass = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Class)).ToList();
        var beforeAssembly = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Assembly)).ToList();
        var afterAssembly = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Assembly)).ToList();
        var beforeSession = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Session)).ToList();
        var afterSession = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Session)).ToList();

        var builder = new StringBuilder();
        builder.AppendLine("new global::NextUnit.Internal.LifecycleInfo");
        builder.AppendLine("                {");

        builder.Append("                    BeforeTestMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeTest);
        builder.AppendLine(",");

        builder.Append("                    AfterTestMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterTest);
        builder.AppendLine(",");

        builder.Append("                    BeforeClassMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeClass);
        builder.AppendLine(",");

        builder.Append("                    AfterClassMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterClass);
        builder.AppendLine(",");

        builder.Append("                    BeforeAssemblyMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeAssembly);
        builder.AppendLine(",");

        builder.Append("                    AfterAssemblyMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterAssembly);
        builder.AppendLine(",");

        builder.Append("                    BeforeSessionMethods = ");
        AppendLifecycleMethodArray(builder, typeName, beforeSession);
        builder.AppendLine(",");

        builder.Append("                    AfterSessionMethods = ");
        AppendLifecycleMethodArray(builder, typeName, afterSession);
        builder.AppendLine();

        builder.Append("                }");
        return builder.ToString();
    }

    private static void AppendLifecycleMethodArray(StringBuilder builder, string typeName, List<LifecycleMethodDescriptor> methods)
    {
        if (methods.Count == 0)
        {
            builder.Append("global::System.Array.Empty<global::NextUnit.Internal.LifecycleMethodDelegate>()");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("                    {");
            foreach (var method in methods)
            {
                builder.AppendLine($"                        {BuildLifecycleMethodDelegate(typeName, method.MethodName, method.IsStatic)},");
            }
            builder.Append("                    }");
        }
    }

    /// <summary>
    /// Builds a dependencies literal.
    /// </summary>
    public static string BuildDependenciesLiteral(ImmutableArray<string> dependencies)
    {
        if (dependencies.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::NextUnit.Internal.TestCaseId>()";
        }

        var builder = new StringBuilder();
        builder.Append("new global::NextUnit.Internal.TestCaseId[] { ");

        for (var i = 0; i < dependencies.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(dependencies[i])})");
        }

        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a string array literal.
    /// </summary>
    public static string BuildStringArrayLiteral(ImmutableArray<string> strings)
    {
        if (strings.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<string>()";
        }

        var builder = new StringBuilder();
        builder.Append("new string[] { ");

        for (var i = 0; i < strings.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(AttributeHelper.ToLiteral(strings[i]));
        }

        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a parameter types literal.
    /// </summary>
    public static string BuildParameterTypesLiteral(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::System.Type>()";
        }

        var builder = new StringBuilder();
        builder.Append("new global::System.Type[] { ");

        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"typeof({parameters[i].Type.ToDisplayString(AttributeHelper.FullyQualifiedTypeFormat)})");
        }

        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a dependency infos literal array.
    /// </summary>
    public static string BuildDependencyInfosLiteral(ImmutableArray<DependencyDescriptor> dependencyInfos)
    {
        if (dependencyInfos.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::NextUnit.Internal.DependencyInfo>()";
        }

        var builder = new StringBuilder();
        builder.Append("new global::NextUnit.Internal.DependencyInfo[] { ");

        for (var i = 0; i < dependencyInfos.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var dep = dependencyInfos[i];
            builder.Append($"new global::NextUnit.Internal.DependencyInfo {{ DependsOnId = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(dep.DependsOnId)}), ProceedOnFailure = {dep.ProceedOnFailure.ToString().ToLowerInvariant()} }}");
        }

        builder.Append(" }");
        return builder.ToString();
    }
}
