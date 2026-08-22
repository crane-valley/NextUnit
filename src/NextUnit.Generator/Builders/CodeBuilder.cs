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

    /// <summary>
    /// Builds the factory the runtime calls to obtain the test class instance.
    /// </summary>
    /// <remarks>
    /// An abstract class gets a factory that fails with the reason instead of no factory at all.
    /// Emitting nothing hands the case to the reflection fallback, which reports whatever
    /// <c>Activator.CreateInstance</c> says about a type the user never asked it to build; naming the
    /// class and the ways out is the same trade <see cref="BuildUnreachableDataSourceProvider"/>
    /// makes. A static test whose class inherits an instance <c>[Before]</c> or <c>[After]</c> hook
    /// reaches it: the hook needs an instance the static test never required.
    /// <para>
    /// A class with no public constructor is treated the same way, and the reflection fallback is
    /// not an answer for it: <c>Type.GetConstructors()</c> returns public constructors only, and the
    /// last resort there is <c>Activator.CreateInstance(Type)</c>, which is public-only as well. The
    /// two agree exactly -- no public constructor means neither path can build the class -- so the
    /// only thing emitting nothing bought was a <c>MissingMethodException</c> naming no test and no
    /// remedy.
    /// </para>
    /// </remarks>
    public static string BuildTestClassFactory(
        string typeName,
        TestClassConstructorKind constructorKind,
        bool requiresInstance)
    {
        if (!requiresInstance)
        {
            return "static (output, context) => null!";
        }

        if (constructorKind == TestClassConstructorKind.Uninstantiable)
        {
            return BuildUnconstructableTestClassFactory(
                $"Test class '{typeName}' is abstract and cannot be instantiated, but a test in it needs " +
                "an instance -- either the test itself, or a test-scoped or class-scoped instance " +
                "lifecycle hook it declares or inherits. Make the hook static, or move the test to a " +
                "concrete class.");
        }

        if (constructorKind == TestClassConstructorKind.None)
        {
            return BuildUnconstructableTestClassFactory(
                $"Test class '{typeName}' has no public constructor that NextUnit can call, but a test in it " +
                "needs an instance -- either the test itself, or a test-scoped or class-scoped instance " +
                "lifecycle hook it declares or inherits. Make the hook static, or add a public constructor.");
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

    /// <summary>
    /// Builds a factory that fails with the reason the class cannot be constructed.
    /// </summary>
    /// <remarks>
    /// A factory that throws rather than no factory at all, so the failure names the test class and
    /// the way out instead of arriving as whatever the reflection fallback says about a type the
    /// user never asked it to build. The same trade
    /// <see cref="BuildUnreachableDataSourceProvider"/> makes.
    /// </remarks>
    private static string BuildUnconstructableTestClassFactory(string message) =>
        "static (output, context) => throw new global::System.InvalidOperationException(" +
        AttributeHelper.ToLiteral(message) +
        ")";

    public static string BuildDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind) =>
        memberKind == DataSourceMemberKind.Unknown
            ? "null"
            : $"static () => (object?){BuildMemberAccess(typeName, memberName, memberKind)}";

    /// <summary>
    /// Builds the synchronous provider for a <c>[TestData]</c> member, which is emitted only for a
    /// synchronous source. An asynchronous member is reached through
    /// <see cref="BuildAsyncTestDataSourceProvider"/> instead, and emitting both would make the
    /// runtime invoke the member twice.
    /// </summary>
    /// <remarks>
    /// The member is handed to <c>DataSourceAdapter.FromEnumerable&lt;TRow&gt;</c> only when the
    /// descriptor names a row type, which it does for a source offering more than one. That call is
    /// where the arm gets chosen: the runtime holds the provider's result as <c>object</c> and reads
    /// it back as a non-generic <c>IEnumerable</c>, so a cast there would select no implementation.
    /// A source offering one row type is passed straight through, since the wrapper would only add a
    /// layer between the runtime and the sole arm it was already reading.
    /// </remarks>
    public static string BuildTestDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape,
        string? rowTypeName)
    {
        if (shape != DataSourceShape.Sync || memberKind == DataSourceMemberKind.Unknown)
        {
            return "null";
        }

        return rowTypeName is null
            ? BuildDataSourceProvider(typeName, memberName, memberKind)
            : "static () => global::NextUnit.Internal.DataSourceAdapter.FromEnumerable" +
                $"<{rowTypeName}>({BuildMemberAccess(typeName, memberName, memberKind)})";
    }

    private static string BuildMemberAccess(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind) =>
        memberKind == DataSourceMemberKind.Method
            ? $"{typeName}.{memberName}()"
            : $"{typeName}.{memberName}";

    /// <summary>
    /// Builds the asynchronous provider for a <c>[TestData]</c> member, or <c>null</c> when the
    /// member is not an asynchronous source the generator can bind.
    /// </summary>
    /// <remarks>
    /// The adapter calls exist because C# has no async iterator lambda, so the conversion to an
    /// untyped row sequence cannot be inlined here. Every emitted call binds its type argument
    /// statically, which is what keeps the generated path free of runtime reflection.
    /// <para>
    /// Only the <c>IAsyncEnumerable&lt;T&gt;</c> arm ever names that type argument in source, and
    /// only for a source that offers more than one. The task-wrapped arms take the awaited
    /// collection type rather than the row type, and <c>Task&lt;TRows&gt;</c> admits exactly one
    /// inference, so there is nothing there for a name to settle.
    /// </para>
    /// </remarks>
    public static string? BuildAsyncTestDataSourceProvider(
        string typeName,
        string memberName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape,
        string? rowTypeName,
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
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromAsyncEnumerableAsync{FormatRowTypeArgument(rowTypeName)}({access}, ct)",
            DataSourceShape.TaskOfCollection =>
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromTaskAsync({access}, ct)",
            DataSourceShape.ValueTaskOfCollection =>
                $"static ct => global::NextUnit.Internal.AsyncDataSourceAdapter.FromTaskAsync(({access}).AsTask(), ct)",
            _ => null
        };
    }

    /// <summary>
    /// Formats the row type argument of the asynchronous enumerable adapter call, which the
    /// descriptor supplies only for a source inference cannot resolve on its own.
    /// </summary>
    /// <remarks>
    /// Naming the row type is what lets a source implementing <c>IAsyncEnumerable&lt;T&gt;</c> more
    /// than once compile at all: inference has two candidates and reports <c>CS0411</c> against the
    /// generated file. It also pins which arm is read to the one
    /// <c>KnownDataSourceTypes.SelectRowType</c> chose, so the rows the run enumerates are the rows
    /// <c>NU0009</c> validated rather than whichever arm inference happened to reach.
    /// </remarks>
    private static string FormatRowTypeArgument(string? rowTypeName) =>
        rowTypeName is null ? "" : $"<{rowTypeName}>";

    /// <summary>
    /// Builds the provider for a data source whose declaring type the generated registry cannot
    /// name.
    /// </summary>
    /// <remarks>
    /// The type appears only inside a string literal, so nothing here needs the visibility the
    /// source lacks. A provider is emitted rather than none, because a source with no provider falls
    /// back to reflecting over the test class, which would read a same-named member of that class
    /// and silently supply the wrong rows. This fails on the row that asked for it and names both
    /// the type and the rule that explains it.
    /// </remarks>
    public static string BuildUnreachableDataSourceProvider(string typeName, string memberName) =>
        "static () => throw new global::System.InvalidOperationException(" +
        AttributeHelper.ToLiteral(
            $"Data source '{memberName}' is declared on '{typeName}', which is not accessible from " +
            "the generated test registry. Make it public, or internal in the test assembly. See NU0020.") +
        ")";

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
