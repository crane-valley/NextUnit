using System.Text;
using NextUnit.CodeAnalysis.Shared;
using NextUnit.Generator.Formatters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Builders;

/// <summary>
/// Builds single-line code literals and delegates for the generated test registry.
/// </summary>
/// <remarks>
/// Everything here is layout-free: multi-line, indented output belongs to the emitters, which own
/// a <see cref="CodeWriter"/> and therefore the nesting context.
/// </remarks>
internal static class CodeBuilder
{
    /// <summary>
    /// Builds a test method delegate.
    /// </summary>
    public static string BuildTestMethodDelegate(
        string typeName,
        string methodName,
        bool isStatic,
        MethodReturnKind returnKind,
        bool acceptsCancellationToken)
    {
        var target = isStatic
            ? $"{typeName}.{methodName}"
            : $"(({typeName})instance).{methodName}";
        var arguments = acceptsCancellationToken ? "ct" : "";
        var invocation = $"{target}({arguments})";

        return BuildMethodDelegate(
            "instance, ct",
            invocation,
            returnKind,
            typeName,
            methodName);
    }

    /// <summary>
    /// Builds a parameterized test method delegate.
    /// </summary>
    public static string BuildParameterizedTestMethodDelegate(
        string typeName,
        string methodName,
        EquatableArray<ParameterDescriptor> parameters,
        EquatableArray<ConstantValue> arguments,
        bool isStatic,
        MethodReturnKind returnKind,
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

            argsBuilder.Append(ArgumentFormatter.FormatArgumentValue(arg, param));
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

        return BuildMethodDelegate(
            "instance, ct",
            invocation,
            returnKind,
            typeName,
            methodName);
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

            var typeName = parameter.TypeofName;
            arguments.Append(
                $"global::NextUnit.Internal.ArgumentConverter.Convert<{typeName}>(arguments[{runtimeArgumentIndex}], {AttributeHelper.ToLiteral(parameter.Name)}, {AttributeHelper.ToLiteral(test.MethodName)})");
            runtimeArgumentIndex++;
        }

        var target = test.IsStatic
            ? $"{test.FullyQualifiedTypeName}.{test.MethodName}"
            : $"(({test.FullyQualifiedTypeName})instance).{test.MethodName}";
        var invocation = $"{target}({arguments})";

        return BuildMethodDelegate(
            "instance, arguments, ct",
            invocation,
            test.ReturnKind,
            test.FullyQualifiedTypeName,
            test.MethodName);
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

    /// <summary>
    /// Builds the synchronous provider for a <c>[TestData]</c> member, which is emitted only for a
    /// synchronous source. An asynchronous member is reached through
    /// <see cref="BuildAsyncTestDataSourceProvider"/> instead, and emitting both would make the
    /// runtime invoke the member twice.
    /// </summary>
    public static string BuildTestDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape) =>
        shape == DataSourceShape.Sync
            ? BuildDataSourceProvider(typeName, memberName, memberKind)
            : "null";

    /// <summary>
    /// Builds the asynchronous provider for a <c>[TestData]</c> member, or <c>null</c> when the
    /// member is not an asynchronous source the generator can bind.
    /// </summary>
    /// <remarks>
    /// The adapter calls exist because C# has no async iterator lambda, so the conversion to an
    /// untyped row sequence cannot be inlined here. Every emitted call binds its type argument
    /// statically, which is what keeps the generated path free of runtime reflection.
    /// </remarks>
    public static string? BuildAsyncTestDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape,
        bool acceptsCancellationToken)
    {
        if (memberKind == DataSourceMemberKind.Unknown)
        {
            return null;
        }

        var arguments = acceptsCancellationToken ? "ct" : "";
        var access = memberKind == DataSourceMemberKind.Method
            ? $"{typeName}.{memberName}({arguments})"
            : $"{typeName}.{memberName}";

        return shape switch
        {
            DataSourceShape.AsyncEnumerable =>
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromAsyncEnumerableAsync({access}, ct)",
            DataSourceShape.TaskOfCollection =>
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromTaskAsync({access}, ct)",
            DataSourceShape.ValueTaskOfCollection =>
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromTaskAsync(({access}).AsTask(), ct)",
            _ => null
        };
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
        MethodReturnKind returnKind,
        bool acceptsCancellationToken)
    {
        var target = isStatic
            ? $"{typeName}.{methodName}"
            : $"(({typeName})instance).{methodName}";
        var arguments = acceptsCancellationToken ? "ct" : "";
        var invocation = $"{target}({arguments})";

        return BuildMethodDelegate(
            "instance, ct",
            invocation,
            returnKind,
            typeName,
            methodName);
    }

    private static string BuildMethodDelegate(
        string parameters,
        string invocation,
        MethodReturnKind returnKind,
        string typeName,
        string methodName)
    {
        return returnKind switch
        {
            MethodReturnKind.Void =>
                $"static ({parameters}) => {{ {invocation}; return global::System.Threading.Tasks.Task.CompletedTask; }}",
            MethodReturnKind.Task =>
                $"static ({parameters}) => {invocation}",
            MethodReturnKind.ValueTask =>
                $"static ({parameters}) => {invocation}.AsTask()",
            _ =>
                $"static ({parameters}) => global::System.Threading.Tasks.Task.FromException(new global::System.InvalidOperationException({AttributeHelper.ToLiteral($"Method '{typeName}.{methodName}' has an unsupported return type.")}))"
        };
    }

    /// <summary>
    /// Builds a dependencies literal.
    /// </summary>
    public static string BuildDependenciesLiteral(EquatableArray<string> dependencies)
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
    public static string BuildStringArrayLiteral(EquatableArray<string> strings)
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
    public static string BuildParameterTypesLiteral(EquatableArray<ParameterDescriptor> parameters)
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

            builder.Append($"typeof({parameters[i].TypeofName})");
        }

        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds a dependency infos literal array.
    /// </summary>
    public static string BuildDependencyInfosLiteral(EquatableArray<DependencyDescriptor> dependencyInfos)
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
            builder.Append($"new global::NextUnit.Internal.DependencyInfo {{ DependsOnId = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(dep.DependsOnId)}), ProceedOnFailure = {LiteralFormatter.Bool(dep.ProceedOnFailure)} }}");
        }

        builder.Append(" }");
        return builder.ToString();
    }
}
