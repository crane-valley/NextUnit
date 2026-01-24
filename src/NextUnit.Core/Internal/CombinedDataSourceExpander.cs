using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="CombinedDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by resolving parameter data sources and computing the Cartesian product at runtime.
/// </summary>
public static class CombinedDataSourceExpander
{
    // Caches for shared instances by sharing scope
    private static readonly ConcurrentDictionary<string, object> _keyedInstances = new();
    private static readonly ConcurrentDictionary<(Type TestClass, Type SourceType), object> _perClassInstances = new();
    private static readonly ConcurrentDictionary<Type, object> _perAssemblyInstances = new();
    private static readonly ConcurrentDictionary<Type, object> _perSessionInstances = new();

    /// <summary>
    /// Expands a collection of combined data source descriptors into test case descriptors.
    /// </summary>
    /// <param name="descriptors">The combined data source descriptors to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(IEnumerable<CombinedDataSourceDescriptor> descriptors)
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
    /// Expands a single combined data source descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The combined data source descriptor to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(CombinedDataSourceDescriptor descriptor)
    {
        // Resolve values for each parameter
        var parameterValues = new List<object?[]>();

        foreach (var source in descriptor.ParameterSources)
        {
            try
            {
                var values = ResolveParameterValues(source, descriptor.TestClass);
                parameterValues.Add(values);
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve values for parameter '{source.ParameterName}' in test '{descriptor.MethodName}'",
                    ex);
            }
        }

        // Compute Cartesian product
        var combinations = ComputeCartesianProduct(parameterValues);

        // Create test cases
        var index = 0;
        foreach (var combination in combinations)
        {
            yield return CreateTestCase(descriptor, combination, index);
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

    private static object?[] ResolveParameterValues(ParameterDataSource source, Type testClass)
    {
        return source.Kind switch
        {
            ParameterDataSourceKind.Inline => source.InlineValues ?? [],
            ParameterDataSourceKind.Member => ResolveMemberValues(source, testClass),
            ParameterDataSourceKind.Class => ResolveClassValues(source, testClass),
            _ => []
        };
    }

    private static object?[] ResolveMemberValues(ParameterDataSource source, Type testClass)
    {
        var memberType = source.MemberType ?? testClass;
        var memberName = source.MemberName
            ?? throw new InvalidOperationException("MemberName is required for ValuesFromMember");

        // Try to find property first
        var property = memberType.GetProperty(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (property is not null)
        {
            var value = property.GetValue(null);
            return EnumerateToArray(value);
        }

        // Try to find field
        var field = memberType.GetField(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (field is not null)
        {
            var value = field.GetValue(null);
            return EnumerateToArray(value);
        }

        // Try to find method
        var method = memberType.GetMethod(
            memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method is not null)
        {
            var value = method.Invoke(null, null);
            return EnumerateToArray(value);
        }

        throw new InvalidOperationException(
            $"Member '{memberName}' not found on type '{memberType.FullName}'. " +
            "The member must be a static property, field, or parameterless method.");
    }

    private static object?[] ResolveClassValues(ParameterDataSource source, Type testClass)
    {
        if (source.ClassDataSourceType is null)
        {
            throw new InvalidOperationException("ClassDataSourceType is required for ValuesFrom");
        }

        var instance = GetOrCreateInstance(
            source.ClassDataSourceType,
            source.SharedType,
            source.SharedKey,
            testClass);

        return EnumerateToArray(instance);
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

    private static object?[] EnumerateToArray(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is object?[] array)
        {
            return array;
        }

        if (value is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().ToArray();
        }

        return [value];
    }

    private static List<object?[]> ComputeCartesianProduct(List<object?[]> parameterValues)
    {
        if (parameterValues.Count == 0)
        {
            return [];
        }

        var result = new List<object?[]>();
        var indices = new int[parameterValues.Count];
        var lengths = parameterValues.Select(v => v.Length).ToArray();

        // Check for empty parameter values
        if (lengths.Any(l => l == 0))
        {
            return [];
        }

        while (true)
        {
            // Build current combination
            var combination = new object?[parameterValues.Count];
            for (var i = 0; i < parameterValues.Count; i++)
            {
                combination[i] = parameterValues[i][indices[i]];
            }
            result.Add(combination);

            // Increment indices (like a multi-digit counter)
            var carry = true;
            for (var i = indices.Length - 1; i >= 0 && carry; i--)
            {
                indices[i]++;
                if (indices[i] >= lengths[i])
                {
                    indices[i] = 0;
                }
                else
                {
                    carry = false;
                }
            }

            // If we carried out of the first position, we're done
            if (carry)
            {
                break;
            }
        }

        return result;
    }

    private static TestCaseDescriptor CreateTestCase(
        CombinedDataSourceDescriptor descriptor,
        object?[] arguments,
        int index)
    {
        // Build unique test ID
        var testId = $"{descriptor.BaseId}:Combined[{index}]";

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
                    parameters[^1].ParameterType == typeof(CancellationToken) &&
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

    /// <summary>
    /// Disposes an instance if it implements IDisposable or IAsyncDisposable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> This method blocks on async disposal using GetAwaiter().GetResult().
    /// In synchronization contexts that don't allow blocking (e.g., UI threads),
    /// this could potentially cause deadlocks. In test frameworks, this is typically safe
    /// as tests run on thread pool threads without special synchronization contexts.
    /// </para>
    /// <para>
    /// If deadlocks occur in production use, consider implementing a fully async cleanup path.
    /// </para>
    /// </remarks>
    private static void DisposeIfNeeded(object instance)
    {
        try
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
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
        catch (OperationCanceledException)
        {
            throw; // Cancellation should propagate
        }
        catch (Exception ex) when (ex is not StackOverflowException)
        {
            // Best-effort disposal: log full exception and continue to avoid failing test cleanup
            Debug.WriteLine($"[NextUnit] Failed to dispose shared instance '{instance.GetType().FullName}': {ex}");
        }
    }
}
