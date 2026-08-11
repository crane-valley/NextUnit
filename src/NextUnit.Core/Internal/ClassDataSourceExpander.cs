using System.Collections;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="ClassDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by instantiating data source classes and enumerating their data at runtime.
/// </summary>
/// <remarks>
/// Shared instances come from <see cref="SharedInstanceStore"/>, which <c>[ValuesFrom]</c> shares
/// through <see cref="CombinedDataSourceExpander"/>: one data source type used through both
/// attributes under the same sharing scope is one instance, and the store disposes it at the end of
/// the session.
/// </remarks>
internal static class ClassDataSourceExpander
{
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
            var instance = SharedInstanceStore.GetOrCreate(
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

        var seed = new TestCaseSeed(descriptor);
        var testMethod = seed.ResolveTestInvoker();

        // Build unique test ID including all source type names
        var combinedSourceTypesName = string.Join("+", descriptor.DataSourceTypes.Select(t => t.Name));
        var idPrefix = $"{descriptor.BaseId}:ClassData:{combinedSourceTypesName}";

        var index = 0;
        foreach (var row in allData)
        {
            yield return seed.CreateTestCase($"{idPrefix}[{index}]", row.Arguments, index, testMethod, row);
            index++;
        }
    }

}
