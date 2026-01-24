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

        var displayName = DisplayNameBuilder.Build(
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
