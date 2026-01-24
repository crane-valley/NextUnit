using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Builders;
using NextUnit.Generator.Formatters;
using NextUnit.Generator.Helpers;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Emitters;

/// <summary>
/// Emits test case and test data descriptor code.
/// </summary>
internal static class TestCaseEmitter
{
    /// <summary>
    /// Emits a test case descriptor.
    /// </summary>
    public static void EmitTestCase(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        ImmutableArray<TypedConstant>? arguments,
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

        builder.AppendLine("            new global::NextUnit.Internal.TestCaseDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                Id = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(testId)}),");
        builder.AppendLine($"                DisplayName = {AttributeHelper.ToLiteral(displayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");

        if (arguments.HasValue)
        {
            builder.AppendLine($"                TestMethod = {CodeBuilder.BuildParameterizedTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.Parameters, arguments.Value, test.IsStatic)},");
        }
        else
        {
            builder.AppendLine($"                TestMethod = {CodeBuilder.BuildTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.IsStatic)},");
        }

        builder.AppendLine($"                Lifecycle = {CodeBuilder.BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        builder.AppendLine($"                    ParallelGroup = {(test.ParallelGroup is not null ? AttributeHelper.ToLiteral(test.ParallelGroup) : "null")},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? AttributeHelper.ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                IsExplicit = {test.IsExplicit.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                ExplicitReason = {(test.ExplicitReason is not null ? AttributeHelper.ToLiteral(test.ExplicitReason) : "null")},");

        if (arguments.HasValue)
        {
            builder.AppendLine($"                Arguments = {ArgumentFormatter.BuildArgumentsLiteral(arguments.Value)},");
        }
        else
        {
            builder.AppendLine("                Arguments = null,");
        }

        builder.AppendLine($"                Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        builder.AppendLine($"                RequiresTestOutput = {test.RequiresTestOutput.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                RequiresTestContext = {test.RequiresTestContext.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                TimeoutMs = {(test.TimeoutMs is int timeout ? timeout.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                RepeatIndex = {(repeatIndex.HasValue ? repeatIndex.Value.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")},");
        builder.AppendLine($"                Priority = {test.Priority.ToString(CultureInfo.InvariantCulture)}");

        builder.AppendLine("            },");
    }

    /// <summary>
    /// Emits a test case descriptor for a matrix test.
    /// </summary>
    public static void EmitMatrixTestCase(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        ImmutableArray<TypedConstant> combination,
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

        builder.AppendLine("            new global::NextUnit.Internal.TestCaseDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                Id = new global::NextUnit.Internal.TestCaseId({AttributeHelper.ToLiteral(testId)}),");
        builder.AppendLine($"                DisplayName = {AttributeHelper.ToLiteral(displayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");
        builder.AppendLine($"                TestMethod = {CodeBuilder.BuildParameterizedTestMethodDelegate(test.FullyQualifiedTypeName, test.MethodName, test.Parameters, combination, test.IsStatic)},");
        builder.AppendLine($"                Lifecycle = {CodeBuilder.BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        builder.AppendLine($"                    ParallelGroup = {(test.ParallelGroup is not null ? AttributeHelper.ToLiteral(test.ParallelGroup) : "null")},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? AttributeHelper.ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                IsExplicit = {test.IsExplicit.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                ExplicitReason = {(test.ExplicitReason is not null ? AttributeHelper.ToLiteral(test.ExplicitReason) : "null")},");
        builder.AppendLine($"                Arguments = {ArgumentFormatter.BuildArgumentsLiteral(combination)},");
        builder.AppendLine($"                Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        builder.AppendLine($"                RequiresTestOutput = {test.RequiresTestOutput.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                RequiresTestContext = {test.RequiresTestContext.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                TimeoutMs = {(test.TimeoutMs is int timeout ? timeout.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                RepeatIndex = {(repeatIndex.HasValue ? repeatIndex.Value.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")},");
        builder.AppendLine($"                Priority = {test.Priority.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine("            },");
    }

    /// <summary>
    /// Emits a test data descriptor for tests using [TestData].
    /// </summary>
    public static void EmitTestDataDescriptor(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        TestDataSource dataSource)
    {
        var dataSourceType = dataSource.MemberTypeName ?? test.FullyQualifiedTypeName;

        builder.AppendLine("            new global::NextUnit.Internal.TestDataDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                BaseId = {AttributeHelper.ToLiteral(test.Id)},");
        builder.AppendLine($"                DisplayName = {AttributeHelper.ToLiteral(test.DisplayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");
        builder.AppendLine($"                DataSourceName = {AttributeHelper.ToLiteral(dataSource.MemberName)},");
        builder.AppendLine($"                DataSourceType = typeof({dataSourceType}),");
        builder.AppendLine($"                ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        builder.AppendLine($"                Lifecycle = {CodeBuilder.BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        builder.AppendLine($"                    ParallelGroup = {(test.ParallelGroup is not null ? AttributeHelper.ToLiteral(test.ParallelGroup) : "null")},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? AttributeHelper.ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                IsExplicit = {test.IsExplicit.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                ExplicitReason = {(test.ExplicitReason is not null ? AttributeHelper.ToLiteral(test.ExplicitReason) : "null")},");
        builder.AppendLine($"                Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        builder.AppendLine($"                RequiresTestOutput = {test.RequiresTestOutput.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                RequiresTestContext = {test.RequiresTestContext.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                TimeoutMs = {(test.TimeoutMs is int timeout ? timeout.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")},");
        builder.AppendLine($"                Priority = {test.Priority.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine("            },");
    }

    /// <summary>
    /// Emits a class data source descriptor for tests using [ClassDataSource&lt;T&gt;].
    /// </summary>
    public static void EmitClassDataSourceDescriptor(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods,
        ImmutableArray<ClassDataSource> classDataSources)
    {
        // Build the DataSourceTypes array literal
        var typesList = string.Join(", ", classDataSources.Select(s => $"typeof({s.TypeName})"));
        var dataSourceTypesLiteral = $"new global::System.Type[] {{ {typesList} }}";

        // Use the first data source's shared type and key (all should be the same from one attribute)
        var firstSource = classDataSources[0];
        var sharedTypeLiteral = firstSource.SharedType switch
        {
            0 => "global::NextUnit.SharedType.None",
            1 => "global::NextUnit.SharedType.Keyed",
            2 => "global::NextUnit.SharedType.PerClass",
            3 => "global::NextUnit.SharedType.PerAssembly",
            4 => "global::NextUnit.SharedType.PerSession",
            _ => "global::NextUnit.SharedType.None"
        };

        builder.AppendLine("            new global::NextUnit.Internal.ClassDataSourceDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                BaseId = {AttributeHelper.ToLiteral(test.Id)},");
        builder.AppendLine($"                DisplayName = {AttributeHelper.ToLiteral(test.DisplayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");
        builder.AppendLine($"                DataSourceTypes = {dataSourceTypesLiteral},");
        builder.AppendLine($"                SharedType = {sharedTypeLiteral},");
        builder.AppendLine($"                SharedKey = {(firstSource.Key is not null ? AttributeHelper.ToLiteral(firstSource.Key) : "null")},");
        builder.AppendLine($"                ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        builder.AppendLine($"                Lifecycle = {CodeBuilder.BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        builder.AppendLine($"                    ParallelGroup = {(test.ParallelGroup is not null ? AttributeHelper.ToLiteral(test.ParallelGroup) : "null")},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? AttributeHelper.ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                IsExplicit = {test.IsExplicit.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                ExplicitReason = {(test.ExplicitReason is not null ? AttributeHelper.ToLiteral(test.ExplicitReason) : "null")},");
        builder.AppendLine($"                Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        builder.AppendLine($"                RequiresTestOutput = {test.RequiresTestOutput.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                RequiresTestContext = {test.RequiresTestContext.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                TimeoutMs = {(test.TimeoutMs is int timeout ? timeout.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")},");
        builder.AppendLine($"                Priority = {test.Priority.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine("            },");
    }

    /// <summary>
    /// Emits a combined data source descriptor for tests using parameter-level data source attributes.
    /// </summary>
    public static void EmitCombinedDataSourceDescriptor(
        StringBuilder builder,
        TestMethodDescriptor test,
        List<LifecycleMethodDescriptor> lifecycleMethods)
    {
        builder.AppendLine("            new global::NextUnit.Internal.CombinedDataSourceDescriptor");
        builder.AppendLine("            {");
        builder.AppendLine($"                BaseId = {AttributeHelper.ToLiteral(test.Id)},");
        builder.AppendLine($"                DisplayName = {AttributeHelper.ToLiteral(test.DisplayName)},");
        builder.AppendLine($"                TestClass = typeof({test.FullyQualifiedTypeName}),");
        builder.AppendLine($"                MethodName = {AttributeHelper.ToLiteral(test.MethodName)},");
        builder.AppendLine($"                ParameterSources = {BuildParameterSourcesLiteral(test)},");
        builder.AppendLine($"                ParameterTypes = {CodeBuilder.BuildParameterTypesLiteral(test.Parameters)},");
        builder.AppendLine($"                Lifecycle = {CodeBuilder.BuildLifecycleInfoLiteral(test.FullyQualifiedTypeName, lifecycleMethods)},");
        builder.AppendLine("                Parallel = new global::NextUnit.Internal.ParallelInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    NotInParallel = {test.NotInParallel.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    ConstraintKeys = {CodeBuilder.BuildStringArrayLiteral(test.ConstraintKeys)},");
        builder.AppendLine($"                    ParallelGroup = {(test.ParallelGroup is not null ? AttributeHelper.ToLiteral(test.ParallelGroup) : "null")},");
        builder.AppendLine($"                    ParallelLimit = {(test.ParallelLimit is int limit ? limit.ToString(CultureInfo.InvariantCulture) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                Dependencies = {CodeBuilder.BuildDependenciesLiteral(test.Dependencies)},");
        builder.AppendLine($"                DependencyInfos = {CodeBuilder.BuildDependencyInfosLiteral(test.DependencyInfos)},");
        builder.AppendLine($"                IsSkipped = {test.IsSkipped.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                SkipReason = {(test.SkipReason is not null ? AttributeHelper.ToLiteral(test.SkipReason) : "null")},");
        builder.AppendLine($"                IsExplicit = {test.IsExplicit.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                ExplicitReason = {(test.ExplicitReason is not null ? AttributeHelper.ToLiteral(test.ExplicitReason) : "null")},");
        builder.AppendLine($"                Categories = {CodeBuilder.BuildStringArrayLiteral(test.Categories)},");
        builder.AppendLine($"                Tags = {CodeBuilder.BuildStringArrayLiteral(test.Tags)},");
        builder.AppendLine($"                RequiresTestOutput = {test.RequiresTestOutput.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                RequiresTestContext = {test.RequiresTestContext.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                TimeoutMs = {(test.TimeoutMs is int timeout ? timeout.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")},");
        builder.AppendLine($"                Priority = {test.Priority.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine("            },");
    }

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

    private static string BuildParameterSourceLiteral(ParameterDataSourceDescriptor source, string testClassName)
    {
        var sb = new StringBuilder();
        sb.Append("new global::NextUnit.Internal.ParameterDataSource { ");
        sb.Append($"ParameterIndex = {source.ParameterIndex.ToString(CultureInfo.InvariantCulture)}, ");
        sb.Append($"ParameterName = {AttributeHelper.ToLiteral(source.ParameterName)}, ");
        sb.Append($"Kind = global::NextUnit.Internal.ParameterDataSourceKind.{source.Kind}, ");

        switch (source.Kind)
        {
            case ParameterDataSourceKind.Inline:
                sb.Append($"InlineValues = {ArgumentFormatter.BuildArgumentsLiteral(source.InlineValues)}, ");
                sb.Append("MemberName = null, ");
                sb.Append("MemberType = null, ");
                sb.Append("ClassDataSourceType = null, ");
                sb.Append("SharedType = global::NextUnit.SharedType.None, ");
                sb.Append("SharedKey = null");
                break;

            case ParameterDataSourceKind.Member:
                sb.Append("InlineValues = null, ");
                sb.Append($"MemberName = {AttributeHelper.ToLiteral(source.MemberName!)}, ");
                var memberType = source.MemberTypeName ?? testClassName;
                sb.Append($"MemberType = typeof({memberType}), ");
                sb.Append("ClassDataSourceType = null, ");
                sb.Append("SharedType = global::NextUnit.SharedType.None, ");
                sb.Append("SharedKey = null");
                break;

            case ParameterDataSourceKind.Class:
                sb.Append("InlineValues = null, ");
                sb.Append("MemberName = null, ");
                sb.Append("MemberType = null, ");
                sb.Append($"ClassDataSourceType = typeof({source.ClassTypeName}), ");
                var sharedTypeLiteral = source.SharedType switch
                {
                    SharedTypeConstants.None => "global::NextUnit.SharedType.None",
                    SharedTypeConstants.Keyed => "global::NextUnit.SharedType.Keyed",
                    SharedTypeConstants.PerClass => "global::NextUnit.SharedType.PerClass",
                    SharedTypeConstants.PerAssembly => "global::NextUnit.SharedType.PerAssembly",
                    SharedTypeConstants.PerSession => "global::NextUnit.SharedType.PerSession",
                    _ => "global::NextUnit.SharedType.None"
                };
                sb.Append($"SharedType = {sharedTypeLiteral}, ");
                sb.Append($"SharedKey = {(source.SharedKey is not null ? AttributeHelper.ToLiteral(source.SharedKey) : "null")}");
                break;
        }

        sb.Append(" }");
        return sb.ToString();
    }
}
