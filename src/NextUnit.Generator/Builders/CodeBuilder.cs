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
    public static string BuildTestMethodDelegate(
        string typeName,
        string methodName,
        bool isStatic,
        bool returnsVoid,
        bool acceptsCancellationToken)
    {
        var target = isStatic
            ? $"{typeName}.{methodName}"
            : $"(({typeName})instance).{methodName}";
        var arguments = acceptsCancellationToken ? "ct" : "";
        var invocation = $"{target}({arguments})";

        return returnsVoid
            ? $"static (instance, ct) => {{ {invocation}; return global::System.Threading.Tasks.Task.CompletedTask; }}"
            : $"static (instance, ct) => {invocation}";
    }

    /// <summary>
    /// Builds a parameterized test method delegate.
    /// </summary>
    public static string BuildParameterizedTestMethodDelegate(
        string typeName,
        string methodName,
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<TypedConstant> arguments,
        bool isStatic,
        bool returnsVoid,
        bool acceptsCancellationToken)
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

        if (acceptsCancellationToken)
        {
            if (argsBuilder.Length > 0)
            {
                argsBuilder.Append(", ");
            }

            argsBuilder.Append("ct");
        }

        var target = isStatic
            ? $"{typeName}.{methodName}"
            : $"(({typeName})instance).{methodName}";
        var invocation = $"{target}({argsBuilder})";

        return returnsVoid
            ? $"static (instance, ct) => {{ {invocation}; return global::System.Threading.Tasks.Task.CompletedTask; }}"
            : $"static (instance, ct) => {invocation}";
    }

    public static string BuildRuntimeParameterizedTestMethodDelegate(TestMethodDescriptor test)
    {
        var arguments = new StringBuilder();
        var runtimeArgumentIndex = 0;

        for (var parameterIndex = 0; parameterIndex < test.Parameters.Length; parameterIndex++)
        {
            var parameter = test.Parameters[parameterIndex];
            var isCancellationToken =
                parameterIndex == test.Parameters.Length - 1 &&
                test.AcceptsCancellationToken;

            if (arguments.Length > 0)
            {
                arguments.Append(", ");
            }

            if (isCancellationToken)
            {
                arguments.Append("ct");
                continue;
            }

            var typeName = parameter.Type.ToDisplayString(AttributeHelper.TypeofCompatibleFormat);
            arguments.Append($"({typeName})arguments[{runtimeArgumentIndex}]!");
            runtimeArgumentIndex++;
        }

        var target = test.IsStatic
            ? $"{test.FullyQualifiedTypeName}.{test.MethodName}"
            : $"(({test.FullyQualifiedTypeName})instance).{test.MethodName}";
        var invocation = $"{target}({arguments})";

        return test.ReturnsVoid
            ? $"static (instance, arguments, ct) => {{ {invocation}; return global::System.Threading.Tasks.Task.CompletedTask; }}"
            : $"static (instance, arguments, ct) => {invocation}";
    }

    public static string BuildTestClassFactory(
        string typeName,
        TestClassConstructorKind constructorKind,
        bool requiresInstance)
    {
        if (!requiresInstance)
        {
            return "static (output, context) => null!";
        }

        return constructorKind switch
        {
            TestClassConstructorKind.Parameterless => $"static (output, context) => new {typeName}()",
            TestClassConstructorKind.Context => $"static (output, context) => new {typeName}(context)",
            TestClassConstructorKind.Output => $"static (output, context) => new {typeName}(output)",
            TestClassConstructorKind.ContextAndOutput => $"static (output, context) => new {typeName}(context, output)",
            TestClassConstructorKind.OutputAndContext => $"static (output, context) => new {typeName}(output, context)",
            _ => "null"
        };
    }

    public static string BuildDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind)
    {
        var access = memberKind == DataSourceMemberKind.Method
            ? $"{typeName}.{memberName}()"
            : $"{typeName}.{memberName}";

        return memberKind == DataSourceMemberKind.Unknown
            ? "null"
            : $"static () => (object?){access}";
    }

    public static string BuildDataSourceFactory(string typeName) =>
        $"static () => new {typeName}()";

    /// <summary>
    /// Builds a lifecycle method delegate.
    /// </summary>
    public static string BuildLifecycleMethodDelegate(
        string typeName,
        string methodName,
        bool isStatic,
        bool returnsVoid,
        bool acceptsCancellationToken)
    {
        var target = isStatic
            ? $"{typeName}.{methodName}"
            : $"(({typeName})instance).{methodName}";
        var arguments = acceptsCancellationToken ? "ct" : "";
        var invocation = $"{target}({arguments})";

        return returnsVoid
            ? $"static (instance, ct) => {{ {invocation}; return global::System.Threading.Tasks.Task.CompletedTask; }}"
            : $"static (instance, ct) => {invocation}";
    }

    /// <summary>
    /// Builds a lifecycle info literal.
    /// Note: Assembly and Session scoped methods are handled globally via GeneratedTestRegistry
    /// static properties, so they are always emitted as empty arrays here.
    /// </summary>
    public static string BuildLifecycleInfoLiteral(string typeName, List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        // Only emit Test and Class scopes - Assembly and Session are handled globally
        var beforeTest = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var afterTest = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Test)).ToList();
        var beforeClass = lifecycleMethods.Where(m => m.BeforeScopes.Contains(LifecycleScopeConstants.Class)).ToList();
        var afterClass = lifecycleMethods.Where(m => m.AfterScopes.Contains(LifecycleScopeConstants.Class)).ToList();

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

        // Assembly and Session scopes are handled globally - always emit empty arrays
        builder.AppendLine("                    BeforeAssemblyMethods = EmptyLifecycleMethods,");
        builder.AppendLine("                    AfterAssemblyMethods = EmptyLifecycleMethods,");
        builder.AppendLine("                    BeforeSessionMethods = EmptyLifecycleMethods,");
        builder.Append("                    AfterSessionMethods = EmptyLifecycleMethods");
        builder.AppendLine();

        builder.Append("                }");
        return builder.ToString();
    }

    private static void AppendLifecycleMethodArray(StringBuilder builder, string typeName, List<LifecycleMethodDescriptor> methods)
    {
        if (methods.Count == 0)
        {
            builder.Append("EmptyLifecycleMethods");
        }
        else
        {
            builder.AppendLine("new global::NextUnit.Internal.LifecycleMethodDelegate[]");
            builder.AppendLine("                    {");
            foreach (var method in methods)
            {
                builder.AppendLine(
                    $"                        {BuildLifecycleMethodDelegate(typeName, method.MethodName, method.IsStatic, method.ReturnsVoid, method.AcceptsCancellationToken)},");
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
            return "EmptyTestCaseIds";
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
            return "EmptyStrings";
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
            return "EmptyTypes";
        }

        var builder = new StringBuilder();
        builder.Append("new global::System.Type[] { ");

        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append($"typeof({parameters[i].Type.ToDisplayString(AttributeHelper.TypeofCompatibleFormat)})");
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
            return "EmptyDependencyInfos";
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
