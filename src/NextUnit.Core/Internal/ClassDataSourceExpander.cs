using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
        var allData = new List<ResolvedTestDataRow>();

        for (var sourceIndex = 0; sourceIndex < descriptor.DataSourceTypes.Length; sourceIndex++)
        {
            var sourceType = descriptor.DataSourceTypes[sourceIndex];
            var factory = sourceIndex < descriptor.DataSourceFactories.Length
                ? descriptor.DataSourceFactories[sourceIndex]
                : null;
            var instance = GetOrCreateInstance(
                sourceType,
                descriptor.SharedType,
                descriptor.SharedKey,
                descriptor.TestClass,
                factory);

            try
            {
                if (instance is IEnumerable nonGeneric)
                {
                    foreach (var item in nonGeneric)
                    {
                        allData.Add(TestDataRowResolver.Resolve(item));
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

        var testMethod = descriptor.TestMethodWithArguments ??
            ReflectionTestInvokerFactory.Create(
                descriptor.TestClass,
                descriptor.MethodName,
                descriptor.ParameterTypes);
        var index = 0;
        foreach (var row in allData)
        {
            yield return CreateTestCase(descriptor, row, testMethod, index);
            index++;
        }
    }

    /// <summary>
    /// Clears all shared instances. Call this at the end of a test session to release resources.
    /// </summary>
    public static void ClearSharedInstances()
    {
        DisposeHelper.DisposeAllIn(_keyedInstances.Values);
        DisposeHelper.DisposeAllIn(_perClassInstances.Values);
        DisposeHelper.DisposeAllIn(_perAssemblyInstances.Values);
        DisposeHelper.DisposeAllIn(_perSessionInstances.Values);

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
                DisposeHelper.DisposeIfNeeded(instance);
            }
        }
    }

    private static object GetOrCreateInstance(
        Type sourceType,
        SharedType sharedType,
        string? key,
        Type testClass,
        DataSourceProviderDelegate? factory)
    {
        return sharedType switch
        {
            SharedType.None => CreateInstance(sourceType, factory),

            SharedType.Keyed => _keyedInstances.GetOrAdd(
                $"{sourceType.FullName}:{key ?? "default"}",
                _ => CreateInstance(sourceType, factory)),

            SharedType.PerClass => _perClassInstances.GetOrAdd(
                (testClass, sourceType),
                _ => CreateInstance(sourceType, factory)),

            SharedType.PerAssembly => _perAssemblyInstances.GetOrAdd(
                sourceType,
                _ => CreateInstance(sourceType, factory)),

            SharedType.PerSession => _perSessionInstances.GetOrAdd(
                sourceType,
                _ => CreateInstance(sourceType, factory)),

            _ => CreateInstance(sourceType, factory)
        };
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "The source generator roots class data source constructors with DynamicDependency.")]
    private static object CreateInstance(
        Type sourceType,
        DataSourceProviderDelegate? factory)
    {
        try
        {
            if (factory is not null)
            {
                return factory()
                    ?? throw new InvalidOperationException(
                        $"Failed to create instance of '{sourceType.FullName}': factory returned null");
            }

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

    private static TestCaseDescriptor CreateTestCase(
        ClassDataSourceDescriptor descriptor,
        ResolvedTestDataRow row,
        TestMethodWithArgumentsDelegate? testMethod,
        int index)
    {
        // Build unique test ID including all source type names
        var combinedSourceTypesName = string.Join("+", descriptor.DataSourceTypes.Select(t => t.Name));
        var testId = $"{descriptor.BaseId}:ClassData:{combinedSourceTypesName}[{index}]";

        var displayName = row.DisplayName ?? DisplayNameBuilder.Build(
            descriptor.MethodName,
            descriptor.CustomDisplayNameTemplate,
            descriptor.DisplayNameFormatterType,
            descriptor.TestClass,
            row.Arguments,
            index);

        return new TestCaseDescriptor
        {
            Id = new TestCaseId(testId),
            DisplayName = displayName,
            TestClass = descriptor.TestClass,
            MethodName = descriptor.MethodName,
            TestMethodWithArguments = testMethod,
            TestClassFactory = descriptor.TestClassFactory,
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

}
