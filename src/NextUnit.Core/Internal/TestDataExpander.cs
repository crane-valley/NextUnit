using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="TestDataDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by invoking data source members at runtime.
/// </summary>
public static class TestDataExpander
{
    /// <summary>
    /// Expands a collection of test data descriptors into test case descriptors.
    /// </summary>
    /// <param name="testDataDescriptors">The test data descriptors to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(IEnumerable<TestDataDescriptor> testDataDescriptors)
    {
        foreach (var descriptor in testDataDescriptors)
        {
            foreach (var testCase in ExpandSingle(descriptor))
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Expands a single test data descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The test data descriptor to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(TestDataDescriptor descriptor)
    {
        var dataSourceType = descriptor.DataSourceType ?? descriptor.TestClass;
        var data = GetTestData(dataSourceType, descriptor.DataSourceName);

        if (data is null)
        {
            // Throwing here to make missing data source explicit to the user
            throw new InvalidOperationException(
                $"Test data source '{descriptor.DataSourceName}' not found in type '{dataSourceType.FullName}'");
        }

        var index = 0;
        foreach (var dataRow in data)
        {
            var row = TestDataRowResolver.Resolve(dataRow);
            var testCase = CreateTestCase(descriptor, row, index);
            yield return testCase;
            index++;
        }
    }

    private static IEnumerable? GetTestData(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type sourceType,
        string memberName)
    {
        try
        {
            // Try to find a static method first
            var method = sourceType.GetMethod(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (method is not null)
            {
                return method.Invoke(null, null) as IEnumerable;
            }

            // Try to find a static property
            var property = sourceType.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (property is not null)
            {
                return property.GetValue(null) as IEnumerable;
            }

            // Try to find a static field
            var field = sourceType.GetField(
                memberName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (field is not null)
            {
                return field.GetValue(null) as IEnumerable;
            }

            return null;
        }
        catch (TargetInvocationException ex)
        {
            // Unwrap and rethrow the inner exception to preserve original error information
            throw new InvalidOperationException(
                $"Failed to get test data from '{memberName}' in type '{sourceType.FullName}'",
                ex.InnerException ?? ex);
        }
        catch (Exception ex)
        {
            // Handle other reflection-related errors
            throw new InvalidOperationException(
                $"Failed to access test data source '{memberName}' in type '{sourceType.FullName}'",
                ex);
        }
    }

    private static TestCaseDescriptor CreateTestCase(
        TestDataDescriptor descriptor,
        ResolvedTestDataRow row,
        int index)
    {
        // Include data source type and name in test ID to ensure uniqueness
        // This handles cases where multiple [TestData] attributes point to identically named members on different classes
        var dataSourceType = descriptor.DataSourceType ?? descriptor.TestClass;
        var testId = $"{descriptor.BaseId}:{dataSourceType.FullName}.{descriptor.DataSourceName}[{index}]";
        var displayName = row.DisplayName ?? DisplayNameBuilder.Build(
            descriptor.MethodName,
            descriptor.CustomDisplayNameTemplate,
            descriptor.DisplayNameFormatterType,
            descriptor.TestClass,
            row.Arguments,
            index);

        // Get the test method via reflection for creating the delegate
        // Specify parameter types to avoid AmbiguousMatchException when method is overloaded
        var methodInfo = descriptor.TestClass.GetMethod(
            descriptor.MethodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: descriptor.ParameterTypes,
            modifiers: null);

        TestMethodDelegate? testMethod = null;

        if (methodInfo is not null)
        {
            testMethod = CreateTestMethodDelegate(methodInfo, row.Arguments);
        }

        return new TestCaseDescriptor
        {
            Id = new TestCaseId(testId),
            DisplayName = displayName,
            TestClass = descriptor.TestClass,
            MethodName = descriptor.MethodName,
            TestMethod = testMethod,
            Lifecycle = descriptor.Lifecycle,
            Parallel = descriptor.Parallel,
            Dependencies = descriptor.Dependencies,
            DependencyInfos = descriptor.DependencyInfos,
            IsSkipped = descriptor.IsSkipped || row.SkipReason is not null,
            SkipReason = descriptor.SkipReason ?? row.SkipReason,
            IsExplicit = descriptor.IsExplicit,
            ExplicitReason = descriptor.ExplicitReason,
            Arguments = row.Arguments,
            Categories = TestDataRowResolver.MergeLabels(descriptor.Categories, row.Categories),
            Tags = TestDataRowResolver.MergeLabels(descriptor.Tags, row.Tags),
            RequiresTestOutput = descriptor.RequiresTestOutput,
            RequiresTestContext = descriptor.RequiresTestContext,
            TimeoutMs = descriptor.TimeoutMs,
            Retry = descriptor.Retry,
            CustomDisplayNameTemplate = descriptor.CustomDisplayNameTemplate,
            DisplayNameFormatterType = descriptor.DisplayNameFormatterType,
            Priority = descriptor.Priority
        };
    }

    private static TestMethodDelegate CreateTestMethodDelegate(MethodInfo methodInfo, object?[] arguments)
    {
        return async (instance, ct) =>
        {
            try
            {
                // Check if the method expects a CancellationToken as the last parameter
                // and we need to append it to the arguments
                var parameters = methodInfo.GetParameters();
                object?[] actualArguments = arguments;
                if (parameters.Length > 0 &&
                    parameters[^1].ParameterType == typeof(System.Threading.CancellationToken) &&
                    arguments.Length < parameters.Length)
                {
                    // Append ct to the arguments array
                    actualArguments = new object?[arguments.Length + 1];
                    arguments.CopyTo(actualArguments, 0);
                    actualArguments[arguments.Length] = ct;
                }

                var result = methodInfo.Invoke(instance, actualArguments);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap the inner exception to preserve the original test failure
                if (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
                throw;
            }
        };
    }
}
