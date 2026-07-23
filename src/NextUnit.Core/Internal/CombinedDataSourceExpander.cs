using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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

        var testMethod = descriptor.TestMethodWithArguments ??
            ReflectionTestInvokerFactory.Create(
                descriptor.TestClass,
                descriptor.MethodName,
                descriptor.ParameterTypes);

        // Create test cases
        var index = 0;
        foreach (var combination in combinations)
        {
            yield return CreateTestCase(descriptor, combination, testMethod, index);
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

    private static object?[] ResolveParameterValues(
        ParameterDataSource source,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type testClass)
    {
        return source.Kind switch
        {
            ParameterDataSourceKind.Inline => source.InlineValues ?? [],
            ParameterDataSourceKind.Member => ResolveMemberValues(source, testClass),
            ParameterDataSourceKind.Class => ResolveClassValues(source, testClass),
            _ => []
        };
    }

    private static object?[] ResolveMemberValues(
        ParameterDataSource source,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type testClass)
    {
        if (source.MemberProvider is not null)
        {
            return EnumerateToArray(source.MemberProvider());
        }

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
            testClass,
            source.ClassDataSourceFactory);

        return EnumerateToArray(instance);
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
        TestMethodWithArgumentsDelegate? testMethod,
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
            IsSkipped = descriptor.IsSkipped,
            SkipReason = descriptor.SkipReason,
            IsExplicit = descriptor.IsExplicit,
            ExplicitReason = descriptor.ExplicitReason,
            Arguments = arguments,
            Categories = descriptor.Categories,
            Tags = descriptor.Tags,
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
