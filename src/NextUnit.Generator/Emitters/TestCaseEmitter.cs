using System.Text;
using NextUnit.Generator.Builders;
using NextUnit.Generator.Formatters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Emitters;

/// <summary>
/// Emits the descriptor initializers that make up the generated registry.
/// </summary>
/// <remarks>
/// The four descriptor kinds differ only in their leading identity and data-source properties;
/// everything from <c>Parallel</c> onwards is identical, so those blocks are emitted once here.
/// The emitted text is pinned byte-for-byte by the generator snapshot tests.
/// </remarks>
internal static class TestCaseEmitter
{
    /// <summary>
    /// Emits a test case descriptor.
    /// </summary>
    public static void EmitTestCase(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        EquatableArray<ConstantValue>? arguments,
        int argumentSetIndex,
        int? repeatIndex = null)
    {
        var testId = test.Id;
        var displayName = test.DisplayName;

        if (arguments.HasValue)
        {
            testId = $"{test.Id}[{argumentSetIndex}]";
            displayName = DisplayNameFormatter.BuildParameterizedDisplayName(test.MethodName, test.CustomDisplayName, arguments.Value);
        }

        // Append repeat index to test ID and display name
        if (repeatIndex.HasValue)
        {
            testId = $"{testId}#{repeatIndex.Value}";
            displayName = $"{displayName} (Repeat #{repeatIndex.Value + 1})";
        }

        BeginDescriptor(writer, "TestCaseDescriptor");
        writer.WriteLine($"Id = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(testId)}),");
        writer.WriteLine($"DisplayName = {AttributeHelper.ToLiteral(displayName)},");
        EmitTestClassBlock(writer, test, lifecycleMethods);

        if (arguments.HasValue)
        {
            writer.WriteLine($"TestMethod = {CodeBuilder.BuildParameterizedTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.Parameters, arguments.Value, test.IsStatic, test.ReturnKind, test.AcceptsCancellationToken)},");
        }
        else
        {
            writer.WriteLine($"TestMethod = {CodeBuilder.BuildTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.IsStatic, test.ReturnKind, test.AcceptsCancellationToken)},");
        }

        EmitLifecycleAndParallelBlock(writer, test, lifecycleMethods);
        EmitDependencyAndSkipBlock(writer, test);

        writer.WriteLine(arguments.HasValue
            ? $"Arguments = {ArgumentFormatter.BuildArgumentsLiteral(arguments.Value)},"
            : "Arguments = null,");

        EmitLabelBlock(writer, test);
        writer.WriteLine($"RepeatIndex = {LiteralFormatter.NullableInt(repeatIndex)},");
        EmitRetryBlock(writer, test);
        EmitCultureBlock(writer, test);
        EmitDisplayNameAndPriorityBlock(writer, test);
        EndDescriptor(writer);
    }

    /// <summary>
    /// Emits a test case descriptor for a matrix test.
    /// </summary>
    public static void EmitMatrixTestCase(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        EquatableArray<ConstantValue> combination,
        int matrixIndex,
        int? repeatIndex = null)
    {
        var testId = $"{test.Id}[M{matrixIndex}]";
        var displayName = DisplayNameFormatter.BuildMatrixDisplayName(test.MethodName, test.CustomDisplayName, test.MatrixParameters, combination);

        // Append repeat index to test ID and display name
        if (repeatIndex.HasValue)
        {
            testId = $"{testId}#{repeatIndex.Value}";
            displayName = $"{displayName} (Repeat #{repeatIndex.Value + 1})";
        }

        BeginDescriptor(writer, "TestCaseDescriptor");
        writer.WriteLine($"Id = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(testId)}),");
        writer.WriteLine($"DisplayName = {AttributeHelper.ToLiteral(displayName)},");
        EmitTestClassBlock(writer, test, lifecycleMethods);
        writer.WriteLine($"TestMethod = {CodeBuilder.BuildParameterizedTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.Parameters, combination, test.IsStatic, test.ReturnKind, test.AcceptsCancellationToken)},");
        EmitLifecycleAndParallelBlock(writer, test, lifecycleMethods);
        EmitDependencyAndSkipBlock(writer, test);
        writer.WriteLine($"Arguments = {ArgumentFormatter.BuildArgumentsLiteral(combination)},");
        EmitLabelBlock(writer, test);
        writer.WriteLine($"RepeatIndex = {LiteralFormatter.NullableInt(repeatIndex)},");
        EmitRetryBlock(writer, test);
        EmitCultureBlock(writer, test);
        EmitDisplayNameAndPriorityBlock(writer, test);
        EndDescriptor(writer);
    }

    /// <summary>
    /// Emits a test data descriptor for tests using [TestData].
    /// </summary>
    public static void EmitTestDataDescriptor(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        TestDataSource dataSource)
    {
        var dataSourceType = dataSource.MemberTypeName ?? test.FullyQualifiedTypeName;

        BeginDescriptor(writer, "TestDataDescriptor");
        EmitRuntimeDescriptorHeader(writer, test, lifecycleMethods);
        writer.WriteLine($"DataSourceName = {AttributeHelper.ToLiteral(dataSource.MemberName)},");
        writer.WriteLine($"DataSourceType = typeof({dataSourceType}),");
        var dataSourceProvider = dataSource.UnreachableMemberTypeName is { } unreachableTypeName
            ? CodeBuilder.BuildUnreachableDataSourceProvider(unreachableTypeName, dataSource.MemberName)
            : CodeBuilder.BuildTestDataSourceProvider(dataSourceType, dataSource.MemberName, dataSource.MemberKind, dataSource.Shape);
        writer.WriteLine($"DataSourceProvider = {dataSourceProvider},");

        // Emitted only for an asynchronous source. The descriptor property already defaults to
        // null, so writing it for every synchronous source would be pure noise in the generated
        // file and would churn every existing snapshot baseline.
        var asyncDataSourceProvider = CodeBuilder.BuildAsyncTestDataSourceProvider(
            dataSourceType,
            dataSource.MemberName,
            dataSource.MemberKind,
            dataSource.Shape,
            dataSource.AcceptsCancellationToken);
        if (asyncDataSourceProvider is not null)
        {
            writer.WriteLine($"AsyncDataSourceProvider = {asyncDataSourceProvider},");
        }

        // Emitted only when opted in, for the same reason as the asynchronous provider above: the
        // descriptor property already defaults to false, and writing it unconditionally would churn
        // every existing snapshot baseline for no gain.
        if (dataSource.DeferredEnumeration)
        {
            writer.WriteLine("DeferredEnumeration = true,");
        }

        writer.WriteLine($"ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        EmitLifecycleAndParallelBlock(writer, test, lifecycleMethods);
        EmitDependencyAndSkipBlock(writer, test);
        EmitLabelBlock(writer, test);
        EmitRetryBlock(writer, test);
        EmitCultureBlock(writer, test);
        EmitDisplayNameAndPriorityBlock(writer, test);
        EndDescriptor(writer);
    }

    /// <summary>
    /// Emits a class data source descriptor for tests using [ClassDataSource&lt;T&gt;].
    /// </summary>
    public static void EmitClassDataSourceDescriptor(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        EquatableArray<ClassDataSource> classDataSources)
    {
        var typesList = string.Join(", ", classDataSources.Select(s => $"typeof({s.TypeName})"));
        var dataSourceTypesLiteral = $"new global::System.Type[] {{ {typesList} }}";
        var factoriesList = string.Join(", ", classDataSources.Select(s => CodeBuilder.BuildDataSourceFactory(s.TypeName)));
        var dataSourceFactoriesLiteral = $"new global::NextUnit.Internal.DataSourceProviderDelegate[] {{ {factoriesList} }}";

        // Use the first data source's shared type and key (all should be the same from one attribute)
        var firstSource = classDataSources[0];

        BeginDescriptor(writer, "ClassDataSourceDescriptor");
        EmitRuntimeDescriptorHeader(writer, test, lifecycleMethods);
        writer.WriteLine($"DataSourceTypes = {dataSourceTypesLiteral},");
        writer.WriteLine($"DataSourceFactories = {dataSourceFactoriesLiteral},");
        writer.WriteLine($"SharedType = {BuildSharedTypeLiteral(firstSource.SharedType)},");
        writer.WriteLine($"SharedKey = {LiteralFormatter.NullableString(firstSource.Key)},");
        writer.WriteLine($"ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        EmitLifecycleAndParallelBlock(writer, test, lifecycleMethods);
        EmitDependencyAndSkipBlock(writer, test);
        EmitLabelBlock(writer, test);
        EmitRetryBlock(writer, test);
        EmitCultureBlock(writer, test);
        EmitDisplayNameAndPriorityBlock(writer, test);
        EndDescriptor(writer);
    }

    /// <summary>
    /// Emits a combined data source descriptor for tests using parameter-level data source attributes.
    /// </summary>
    public static void EmitCombinedDataSourceDescriptor(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        BeginDescriptor(writer, "CombinedDataSourceDescriptor");
        EmitRuntimeDescriptorHeader(writer, test, lifecycleMethods);
        writer.WriteLine($"ParameterSources = {BuildParameterSourcesLiteral(test)},");
        writer.WriteLine($"ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        EmitLifecycleAndParallelBlock(writer, test, lifecycleMethods);
        EmitDependencyAndSkipBlock(writer, test);
        EmitLabelBlock(writer, test);
        EmitRetryBlock(writer, test);
        EmitCultureBlock(writer, test);
        EmitDisplayNameAndPriorityBlock(writer, test);
        EndDescriptor(writer);
    }

    private static void BeginDescriptor(CodeWriter writer, string descriptorTypeName)
    {
        writer.WriteLine($"new global::NextUnit.Internal.{descriptorTypeName}");
        writer.WriteLine("{");
        writer.Indent();
    }

    private static void EndDescriptor(CodeWriter writer)
    {
        writer.Unindent();
        writer.WriteLine("},");
    }

    /// <summary>
    /// Emits the identity and invocation properties shared by the three runtime-expanded descriptors.
    /// </summary>
    private static void EmitRuntimeDescriptorHeader(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        writer.WriteLine($"BaseId = {AttributeHelper.ToLiteral(test.Id)},");
        writer.WriteLine($"DisplayName = {AttributeHelper.ToLiteral(test.DisplayName)},");
        EmitTestClassBlock(writer, test, lifecycleMethods);
        writer.WriteLine($"TestMethodWithArguments = {CodeBuilder.BuildRuntimeParameterizedTestMethodDelegate(test)},");
    }

    private static void EmitTestClassBlock(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        writer.WriteLine($"TestClass = typeof({test.FullyQualifiedTypeName}),");
        writer.WriteLine($"MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");
        writer.WriteLine($"TestClassFactory = {BuildTestClassFactory(test, lifecycleMethods)},");
    }

    private static void EmitLifecycleAndParallelBlock(
        CodeWriter writer,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        writer.Write("Lifecycle = ");
        LifecycleEmitter.EmitLifecycleInfo(writer, test.FullyQualifiedTypeName, lifecycleMethods);
        writer.WriteLine(",");

        writer.WriteLine("Parallel = new global::NextUnit.Internal.ParallelInfo");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"NotInParallel = {LiteralFormatter.Bool(test.NotInParallel)},");
        writer.WriteLine($"ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        writer.WriteLine($"ParallelGroup = {LiteralFormatter.NullableString(test.ParallelGroup)},");
        writer.WriteLine($"ParallelLimit = {LiteralFormatter.NullableInt(test.ParallelLimit)}");
        writer.Unindent();
        writer.WriteLine("},");
    }

    private static void EmitDependencyAndSkipBlock(CodeWriter writer, TestMethodDescriptor test)
    {
        writer.WriteLine($"Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        writer.WriteLine($"DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        writer.WriteLine($"IsSkipped = {LiteralFormatter.Bool(test.IsSkipped)},");
        writer.WriteLine($"SkipReason = {LiteralFormatter.NullableString(test.SkipReason)},");
        writer.WriteLine($"IsExplicit = {LiteralFormatter.Bool(test.IsExplicit)},");
        writer.WriteLine($"ExplicitReason = {LiteralFormatter.NullableString(test.ExplicitReason)},");
    }

    private static void EmitLabelBlock(CodeWriter writer, TestMethodDescriptor test)
    {
        writer.WriteLine($"Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        writer.WriteLine($"Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        writer.WriteLine($"RequiresTestOutput = {LiteralFormatter.Bool(test.RequiresTestOutput)},");
        writer.WriteLine($"RequiresTestContext = {LiteralFormatter.Bool(test.RequiresTestContext)},");
        writer.WriteLine($"TimeoutMs = {LiteralFormatter.NullableInt(test.TimeoutMs)},");
    }

    private static void EmitRetryBlock(CodeWriter writer, TestMethodDescriptor test)
    {
        writer.WriteLine("Retry = new global::NextUnit.Internal.RetryInfo");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"Count = {LiteralFormatter.NullableInt(test.RetryCount)},");
        writer.WriteLine($"DelayMs = {LiteralFormatter.Int(test.RetryDelayMs)},");

        // Emitted only when [Retry<TPolicy>] supplies a policy, for the same reason as the
        // asynchronous data source provider: the descriptor property already defaults to null, and
        // writing it for every test would churn every existing snapshot baseline for no gain. The
        // constructor call is direct, so nothing on this path reflects over the policy type.
        if (!string.IsNullOrEmpty(test.RetryPolicyTypeName))
        {
            writer.WriteLine($"PolicyFactory = static () => new {test.RetryPolicyTypeName}(),");
        }

        writer.WriteLine($"IsFlaky = {LiteralFormatter.Bool(test.IsFlaky)},");
        writer.WriteLine($"FlakyReason = {LiteralFormatter.NullableString(test.FlakyReason)}");
        writer.Unindent();
        writer.WriteLine("},");
    }

    /// <summary>
    /// Emits the declared cultures, and nothing at all when none are declared.
    /// </summary>
    /// <remarks>
    /// Conditional for the same reason as <c>PolicyFactory</c> above: the descriptor property
    /// already defaults to the shared empty instance, so emitting it for every test would churn
    /// every existing snapshot baseline without changing behavior.
    /// </remarks>
    private static void EmitCultureBlock(CodeWriter writer, TestMethodDescriptor test)
    {
        if (test.CultureName is null && test.UICultureName is null)
        {
            return;
        }

        writer.WriteLine("Culture = new global::NextUnit.Internal.TestCultureInfo");
        writer.WriteLine("{");
        writer.Indent();
        writer.WriteLine($"CultureName = {LiteralFormatter.NullableString(test.CultureName)},");
        writer.WriteLine($"UICultureName = {LiteralFormatter.NullableString(test.UICultureName)}");
        writer.Unindent();
        writer.WriteLine("},");
    }

    private static void EmitDisplayNameAndPriorityBlock(CodeWriter writer, TestMethodDescriptor test)
    {
        writer.WriteLine($"CustomDisplayNameTemplate = {LiteralFormatter.NullableString(test.CustomDisplayName)},");
        writer.WriteLine($"DisplayNameFormatterType = {LiteralFormatter.NullableTypeof(test.DisplayNameFormatterType)},");
        writer.WriteLine($"Priority = {LiteralFormatter.Int(test.Priority)}");
    }

    private static string BuildSharedTypeLiteral(int sharedType) => sharedType switch
    {
        SharedTypeConstants.None => "global::NextUnit.SharedType.None",
        SharedTypeConstants.Keyed => "global::NextUnit.SharedType.Keyed",
        SharedTypeConstants.PerClass => "global::NextUnit.SharedType.PerClass",
        SharedTypeConstants.PerAssembly => "global::NextUnit.SharedType.PerAssembly",
        SharedTypeConstants.PerSession => "global::NextUnit.SharedType.PerSession",
        _ => "global::NextUnit.SharedType.None"
    };

    private static string BuildParameterSourcesLiteral(TestMethodDescriptor test)
    {
        if (test.CombinedParameterSources.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<global::NextUnit.Internal.ParameterDataSource>()";
        }

        var sb = new StringBuilder();
        sb.Append("new global::NextUnit.Internal.ParameterDataSource[] { ");

        for (var i = 0; i < test.CombinedParameterSources.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var source = test.CombinedParameterSources[i];
            sb.Append(BuildParameterSourceLiteral(source, test.FullyQualifiedTypeName));
        }

        sb.Append(" }");
        return sb.ToString();
    }

    private static string BuildTestClassFactory(
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        var requiresInstance = !test.IsStatic ||
            lifecycleMethods.Any(static lifecycle =>
                !lifecycle.IsStatic &&
                (lifecycle.BeforeScopes.Contains(LifecycleScopeConstants.Test) ||
                 lifecycle.AfterScopes.Contains(LifecycleScopeConstants.Test) ||
                 lifecycle.BeforeScopes.Contains(LifecycleScopeConstants.Class) ||
                 lifecycle.AfterScopes.Contains(LifecycleScopeConstants.Class)));

        return CodeBuilder.BuildTestClassFactory(
            test.FullyQualifiedTypeName,
            test.ConstructorKind,
            requiresInstance);
    }

    private static string BuildParameterSourceLiteral(ParameterDataSourceDescriptor source, string testClassName)
    {
        var sb = new StringBuilder();
        sb.Append("new global::NextUnit.Internal.ParameterDataSource { ");
        sb.Append($"ParameterIndex = {LiteralFormatter.Int(source.ParameterIndex)}, ");
        sb.Append($"ParameterName = {AttributeHelper.ToLiteral(source.ParameterName)}, ");
        sb.Append($"Kind = global::NextUnit.Internal.ParameterDataSourceKind.{source.Kind}, ");

        switch (source.Kind)
        {
            case ParameterDataSourceKind.Inline:
                sb.Append($"InlineValues = {ArgumentFormatter.BuildArgumentsLiteral(source.InlineValues)}, ");
                sb.Append("MemberName = null, ");
                sb.Append("MemberType = null, ");
                sb.Append("MemberProvider = null, ");
                sb.Append("ClassDataSourceType = null, ");
                sb.Append("ClassDataSourceFactory = null, ");
                sb.Append("SharedType = global::NextUnit.SharedType.None, ");
                sb.Append("SharedKey = null");
                break;

            case ParameterDataSourceKind.Member:
                sb.Append("InlineValues = null, ");
                sb.Append($"MemberName = {AttributeHelper.ToLiteral(source.MemberName!)}, ");
                var memberType = source.MemberTypeName ?? testClassName;
                sb.Append($"MemberType = typeof({memberType}), ");
                var memberProvider = source.UnreachableMemberTypeName is { } unreachableMemberType
                    ? CodeBuilder.BuildUnreachableDataSourceProvider(unreachableMemberType, source.MemberName!)
                    : CodeBuilder.BuildDataSourceProvider(memberType, source.MemberName!, source.MemberKind);
                sb.Append($"MemberProvider = {memberProvider}, ");
                sb.Append("ClassDataSourceType = null, ");
                sb.Append("ClassDataSourceFactory = null, ");
                sb.Append("SharedType = global::NextUnit.SharedType.None, ");
                sb.Append("SharedKey = null");
                break;

            case ParameterDataSourceKind.Class:
                sb.Append("InlineValues = null, ");
                sb.Append("MemberName = null, ");
                sb.Append("MemberType = null, ");
                sb.Append("MemberProvider = null, ");
                sb.Append($"ClassDataSourceType = typeof({source.ClassTypeName}), ");
                sb.Append($"ClassDataSourceFactory = {CodeBuilder.BuildDataSourceFactory(source.ClassTypeName!)}, ");
                sb.Append($"SharedType = {BuildSharedTypeLiteral(source.SharedType)}, ");
                sb.Append($"SharedKey = {LiteralFormatter.NullableString(source.SharedKey)}");
                break;
        }

        sb.Append(" }");
        return sb.ToString();
    }
}
