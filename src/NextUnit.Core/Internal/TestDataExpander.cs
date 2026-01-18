using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
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
            var arguments = ConvertToObjectArray(dataRow);
            var testCase = CreateTestCase(descriptor, arguments, index);
            yield return testCase;
            index++;
        }
    }

    private static IEnumerable? GetTestData(Type sourceType, string memberName)
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

    private static object?[] ConvertToObjectArray(object? dataRow)
    {
        return dataRow switch
        {
            null => [],
            object?[] array => array,
            IEnumerable enumerable => enumerable.Cast<object?>().ToArray(),
            _ => [dataRow]
        };
    }

    private static TestCaseDescriptor CreateTestCase(
        TestDataDescriptor descriptor,
        object?[] arguments,
        int index)
    {
        // Include data source type and name in test ID to ensure uniqueness
        // This handles cases where multiple [TestData] attributes point to identically named members on different classes
        var dataSourceType = descriptor.DataSourceType ?? descriptor.TestClass;
        var testId = $"{descriptor.BaseId}:{dataSourceType.FullName}.{descriptor.DataSourceName}[{index}]";
        var displayName = BuildDisplayName(
            descriptor.MethodName,
            descriptor.CustomDisplayNameTemplate,
            descriptor.DisplayNameFormatterType,
            descriptor.TestClass,
            arguments,
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
            testMethod = CreateTestMethodDelegate(methodInfo, arguments);
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
            IsSkipped = descriptor.IsSkipped,
            SkipReason = descriptor.SkipReason,
            Arguments = arguments,
            Categories = descriptor.Categories,
            Tags = descriptor.Tags,
            RequiresTestOutput = descriptor.RequiresTestOutput,
            RequiresTestContext = descriptor.RequiresTestContext,
            TimeoutMs = descriptor.TimeoutMs,
            Retry = descriptor.Retry,
            CustomDisplayNameTemplate = descriptor.CustomDisplayNameTemplate,
            DisplayNameFormatterType = descriptor.DisplayNameFormatterType
        };
    }

    private static readonly ConcurrentDictionary<Type, IDisplayNameFormatter> _formatterCache = new();

    private static string BuildDisplayName(
        string methodName,
        string? customDisplayNameTemplate,
        Type? formatterType,
        Type testClass,
        object?[] arguments,
        int argumentSetIndex)
    {
        // Priority 1: Custom formatter
        if (formatterType is not null)
        {
            try
            {
                var formatter = GetFormatter(formatterType);
                var context = new DisplayNameContext
                {
                    MethodName = methodName,
                    TestClass = testClass,
                    Arguments = arguments,
                    ArgumentSetIndex = argumentSetIndex
                };
                return formatter.Format(context);
            }
            catch (InvalidOperationException ex)
            {
                // Log formatter failure but continue with fallback to default display name
                Debug.WriteLine($"[NextUnit] DisplayNameFormatter '{formatterType.FullName}' failed: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                // Log formatter failure but continue with fallback to default display name
                Debug.WriteLine($"[NextUnit] DisplayNameFormatter '{formatterType.FullName}' failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        // Priority 2: Custom template with placeholders
        if (customDisplayNameTemplate is not null)
        {
            return FormatDisplayNameWithPlaceholders(customDisplayNameTemplate, arguments);
        }

        // Priority 3: Default formatting
        if (arguments.Length == 0)
        {
            return methodName;
        }

        var formattedArgs = string.Join(", ", arguments.Select(FormatArgument));
        return $"{methodName}({formattedArgs})";
    }

    private static IDisplayNameFormatter GetFormatter(Type formatterType)
    {
        return _formatterCache.GetOrAdd(formatterType, t =>
            (IDisplayNameFormatter)Activator.CreateInstance(t)!);
    }

    private static string FormatDisplayNameWithPlaceholders(string template, object?[] arguments)
    {
        var result = template;
        for (var i = 0; i < arguments.Length; i++)
        {
            var placeholder = $"{{{i}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, FormatArgument(arguments[i]));
            }
        }
        return result;
    }

    private static string FormatArgument(object? arg)
    {
        return arg switch
        {
            null => "null",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            IEnumerable enumerable when arg is not string => FormatEnumerable(enumerable),
            _ => arg.ToString() ?? "null"
        };
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        // Take 4 items to check if there are more than 3
        var items = enumerable.Cast<object?>().Take(4).ToList();
        var formatted = string.Join(", ", items.Take(3).Select(FormatArgument));

        if (items.Count > 3)
        {
            formatted += ", ...";
        }

        return $"[{formatted}]";
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
