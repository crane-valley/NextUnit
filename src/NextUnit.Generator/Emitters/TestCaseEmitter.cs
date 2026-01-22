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
        int argumentSetIndex)
    {
        var testId = test.Id;
        var displayName = test.DisplayName;

        if (arguments.HasValue)
        {
            testId = $"{test.Id}[{argumentSetIndex}]";
            displayName = DisplayNameFormatter.BuildParameterizedDisplayName(test.MethodName, test.CustomDisplayName, arguments.Value);
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
        builder.AppendLine("                Retry = new global::NextUnit.Internal.RetryInfo");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Count = {(test.RetryCount is int retryCount ? retryCount.ToString(CultureInfo.InvariantCulture) : "null")},");
        builder.AppendLine($"                    DelayMs = {test.RetryDelayMs.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"                    IsFlaky = {test.IsFlaky.ToString().ToLowerInvariant()},");
        builder.AppendLine($"                    FlakyReason = {(test.FlakyReason is not null ? AttributeHelper.ToLiteral(test.FlakyReason) : "null")}");
        builder.AppendLine("                },");
        builder.AppendLine($"                CustomDisplayNameTemplate = {(test.CustomDisplayName is not null ? AttributeHelper.ToLiteral(test.CustomDisplayName) : "null")},");
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")}");

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
        builder.AppendLine($"                DisplayNameFormatterType = {(test.DisplayNameFormatterType is not null ? $"typeof({test.DisplayNameFormatterType})" : "null")}");
        builder.AppendLine("            },");
    }
}
