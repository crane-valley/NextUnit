using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="CombinedDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by resolving parameter data sources and computing the Cartesian product at runtime.
/// </summary>
/// <remarks>
/// Shared instances come from <see cref="SharedInstanceStore"/>, which <c>[ClassDataSource]</c>
/// shares through <see cref="ClassDataSourceExpander"/>: one data source type used through both
/// attributes under the same sharing scope is one instance, and the store disposes it at the end of
/// the session.
/// </remarks>
internal static class CombinedDataSourceExpander
{
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

        var seed = new TestCaseSeed(descriptor);
        var testMethod = seed.ResolveTestInvoker();

        // Create test cases
        var index = 0;
        foreach (var combination in combinations)
        {
            yield return seed.CreateTestCase($"{descriptor.BaseId}:Combined[{index}]", combination, index, testMethod);
            index++;
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

        // Which member the name means is decided by DataSourceMemberLookup, which walks the base
        // chain the way C# does. Searching kind by kind here instead -- every property, then every
        // field, then every method -- would read a base property for a name a derived method has
        // taken over once the hierarchy is in scope.
        if (DataSourceMemberLookup.TryReadStaticMember(memberType, memberName, out var value))
        {
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

        var instance = SharedInstanceStore.GetOrCreate(
            source.ClassDataSourceType,
            source.SharedType,
            source.SharedKey,
            testClass,
            source.ClassDataSourceFactory);

        return EnumerateToArray(instance);
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

}
