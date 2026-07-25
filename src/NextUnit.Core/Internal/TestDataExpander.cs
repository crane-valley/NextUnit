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
        var data = descriptor.DataSourceProvider?.Invoke() as IEnumerable ??
            GetTestData(dataSourceType, descriptor.DataSourceName);

        if (data is null)
        {
            // Throwing here to make missing data source explicit to the user
            throw new InvalidOperationException(
                $"Test data source '{descriptor.DataSourceName}' not found in type '{dataSourceType.FullName}'");
        }

        var seed = new TestCaseSeed(descriptor);
        var testMethod = seed.ResolveTestInvoker();

        // Include data source type and name in test ID to ensure uniqueness
        // This handles cases where multiple [TestData] attributes point to identically named members on different classes
        var idPrefix = $"{descriptor.BaseId}:{dataSourceType.FullName}.{descriptor.DataSourceName}";

        var index = 0;
        foreach (var dataRow in data)
        {
            var row = TestDataRowResolver.Resolve(dataRow);
            yield return seed.CreateTestCase($"{idPrefix}[{index}]", row.Arguments, index, testMethod, row);
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

}
