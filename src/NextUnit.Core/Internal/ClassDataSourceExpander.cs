using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="ClassDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by instantiating data source classes and enumerating their data at runtime.
/// </summary>
public static class ClassDataSourceExpander
{
    // Caches for shared instances by sharing scope
    private static readonly ConcurrentDictionary<string, object> _keyedInstances = new();
    private static readonly ConcurrentDictionary<(Type TestClass, Type SourceType), object> _perClassInstances = new();
    private static readonly ConcurrentDictionary<Type, object> _perAssemblyInstances = new();
    private static readonly ConcurrentDictionary<Type, object> _perSessionInstances = new();

    // Cache for display name formatters
    private static readonly ConcurrentDictionary<Type, IDisplayNameFormatter> _formatterCache = new();

    /// <summary>
    /// Expands a collection of class data source descriptors into test case descriptors.
    /// </summary>
    /// <param name="descriptors">The class data source descriptors to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(IEnumerable<ClassDataSourceDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            foreach (var testCase in ExpandSingle(descriptor))
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Expands a single class data source descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The class data source descriptor to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(ClassDataSourceDescriptor descriptor)
    {
        // Combine data from all source types
        var allData = new List<object?[]>();

        foreach (var sourceType in descriptor.DataSourceTypes)
        {
            var instance = GetOrCreateInstance(
                sourceType,
                descriptor.SharedType,
                descriptor.SharedKey,
                descriptor.TestClass);

            try
            {
                if (instance is IEnumerable<object?[]> typedEnumerable)
                {
                    allData.AddRange(typedEnumerable);
                }
                else if (instance is IEnumerable nonGeneric)
                {
                    foreach (var item in nonGeneric)
                    {
                        allData.Add(ConvertToObjectArray(item));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to enumerate test data from class '{sourceType.FullName}'",
                    ex);
            }
        }

        var index = 0;
        foreach (var arguments in allData)
        {
            yield return CreateTestCase(descriptor, arguments, index);
            index++;
        }
    }

    /// <summary>
    /// Clears all shared instances. Call this at the end of a test session to release resources.
    /// </summary>
    public static void ClearSharedInstances()
    {
        DisposeAllIn(_keyedInstances.Values);
        DisposeAllIn(_perClassInstances.Values);
        DisposeAllIn(_perAssemblyInstances.Values);
        DisposeAllIn(_perSessionInstances.Values);

        _keyedInstances.Clear();
        _perClassInstances.Clear();
        _perAssemblyInstances.Clear();
        _perSessionInstances.Clear();
    }

    /// <summary>
    /// Clears class-level shared instances for the specified test class.
    /// Call this after all tests in a class have completed.
    /// </summary>
    /// <param name="testClass">The test class whose instances should be cleared.</param>
    public static void ClearClassInstances(Type testClass)
    {
        var keysToRemove = _perClassInstances.Keys
            .Where(k => k.TestClass == testClass)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_perClassInstances.TryRemove(key, out var instance))
            {
                DisposeIfNeeded(instance);
            }
        }
    }

    private static object GetOrCreateInstance(
        Type sourceType,
        SharedType sharedType,
        string? key,
        Type testClass)
    {
        return sharedType switch
        {
            SharedType.None => CreateInstance(sourceType),

            SharedType.Keyed => _keyedInstances.GetOrAdd(
                $"{sourceType.FullName}:{key ?? "default"}",
                _ => CreateInstance(sourceType)),

            SharedType.PerClass => _perClassInstances.GetOrAdd(
                (testClass, sourceType),
                _ => CreateInstance(sourceType)),

            SharedType.PerAssembly => _perAssemblyInstances.GetOrAdd(
                sourceType,
                _ => CreateInstance(sourceType)),

            SharedType.PerSession => _perSessionInstances.GetOrAdd(
                sourceType,
                _ => CreateInstance(sourceType)),

            _ => CreateInstance(sourceType)
        };
    }

    private static object CreateInstance(Type sourceType)
    {
        try
        {
            return Activator.CreateInstance(sourceType)
                ?? throw new InvalidOperationException(
                    $"Failed to create instance of '{sourceType.FullName}': Activator returned null");
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of '{sourceType.FullName}'",
                ex.InnerException ?? ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of '{sourceType.FullName}'",
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
        ClassDataSourceDescriptor descriptor,
        object?[] arguments,
        int index)
    {
        // Build unique test ID including all source type names
        var combinedSourceTypesName = string.Join("+", descriptor.DataSourceTypes.Select(t => t.Name));
        var testId = $"{descriptor.BaseId}:ClassData:{combinedSourceTypesName}[{index}]";

        var displayName = BuildDisplayName(
            descriptor.MethodName,
            descriptor.CustomDisplayNameTemplate,
            descriptor.DisplayNameFormatterType,
            descriptor.TestClass,
            arguments,
            index);

        // Get the test method via reflection for creating the delegate
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
            DependencyInfos = descriptor.DependencyInfos,
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
                Debug.WriteLine($"[NextUnit] DisplayNameFormatter '{formatterType.FullName}' failed: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
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
        {
            var instance = Activator.CreateInstance(t)
                ?? throw new InvalidOperationException(
                    $"Failed to create display name formatter of type '{t.FullName}'. " +
                    "Ensure the type has a public parameterless constructor.");

            return instance as IDisplayNameFormatter
                ?? throw new InvalidOperationException(
                    $"Type '{t.FullName}' must implement IDisplayNameFormatter " +
                    "to be used as a display name formatter.");
        });
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
        var items = enumerable.Cast<object?>().Take(4).ToList();

        // Use at most three items from the already materialized list
        var displayCount = Math.Min(3, items.Count);
        var formatted = string.Join(", ", items.GetRange(0, displayCount).Select(FormatArgument));

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
                var parameters = methodInfo.GetParameters();
                object?[] actualArguments = arguments;

                // Check if the method expects a CancellationToken as the last parameter
                // and arguments array has exactly one fewer element (the CancellationToken slot)
                if (parameters.Length > 0 &&
                    parameters[^1].ParameterType == typeof(System.Threading.CancellationToken) &&
                    arguments.Length == parameters.Length - 1)
                {
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
                if (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
                throw;
            }
        };
    }

    private static void DisposeAllIn(IEnumerable<object> instances)
    {
        foreach (var instance in instances)
        {
            DisposeIfNeeded(instance);
        }
    }

    private static void DisposeIfNeeded(object instance)
    {
        try
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
                // Note: Blocking on async disposal is necessary here as this is called during cleanup.
                // In production, consider implementing async cleanup patterns if deadlocks occur.
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            else if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (OutOfMemoryException)
        {
            throw; // Fatal exception - do not swallow
        }
        catch (Exception ex)
        {
            // Best-effort disposal: log and continue to avoid failing test cleanup
            Debug.WriteLine($"[NextUnit] Failed to dispose shared instance '{instance.GetType().FullName}': {ex.Message}");
        }
    }
}
