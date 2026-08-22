using System.Collections;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="ClassDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by instantiating data source classes and enumerating their data at runtime.
/// </summary>
/// <remarks>
/// <c>[Repeat]</c> multiplies the rows here rather than in the generator. The two compile-time
/// buckets expand it by emitting one test case per iteration, but this descriptor is emitted once
/// per attribute and its rows appear only once the source types are instantiated, so the count rides
/// on the descriptor and is applied per row.
/// <para>
/// Shared instances come from <see cref="SharedInstanceStore"/>, which <c>[ValuesFrom]</c> shares
/// through <see cref="CombinedDataSourceExpander"/>: one data source type used through both
/// attributes under the same sharing scope is one instance, and the store disposes it at the end of
/// the session.
/// </para>
/// </remarks>
internal static class ClassDataSourceExpander
{
    /// <summary>
    /// Expands a collection of class data source descriptors into test case descriptors.
    /// </summary>
    /// <param name="descriptors">The class data source descriptors to expand.</param>
    /// <param name="registryMaxTestCasesPerMethod">
    /// The cap carried by the registry these descriptors came from, or <see langword="null"/> when
    /// the caller has no registry to read it from.
    /// </param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(
        IEnumerable<ClassDataSourceDescriptor> descriptors,
        int? registryMaxTestCasesPerMethod = null)
    {
        foreach (var descriptor in descriptors)
        {
            foreach (var testCase in ExpandSingle(descriptor, registryMaxTestCasesPerMethod))
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Expands a single class data source descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The class data source descriptor to expand.</param>
    /// <param name="registryMaxTestCasesPerMethod">
    /// The cap carried by the registry this descriptor came from, or <see langword="null"/> when the
    /// caller has no registry to read it from, in which case the built-in default applies.
    /// </param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(
        ClassDataSourceDescriptor descriptor,
        int? registryMaxTestCasesPerMethod = null)
    {
        // Checked before the source types are instantiated: a refused expansion must not first run
        // the user's data source classes and dispose of nothing.
        TestCaseExpansionLimits.EnsureRepeatWithinLimit(
            descriptor.MethodName,
            descriptor.RepeatCount,
            registryMaxTestCasesPerMethod);

        // Combine data from all source types
        var allData = new List<ResolvedTestDataRow>();

        for (var sourceIndex = 0; sourceIndex < descriptor.DataSourceTypes.Length; sourceIndex++)
        {
            var sourceType = descriptor.DataSourceTypes[sourceIndex];
            var factory = sourceIndex < descriptor.DataSourceFactories.Length
                ? descriptor.DataSourceFactories[sourceIndex]
                : null;
            var reader = sourceIndex < descriptor.DataSourceRowReaders.Length
                ? descriptor.DataSourceRowReaders[sourceIndex]
                : null;
            var instance = SharedInstanceStore.GetOrCreate(
                sourceType,
                descriptor.SharedType,
                descriptor.SharedKey,
                descriptor.TestClass,
                factory);

            try
            {
                // The reader is a read, not a handover: the store created this instance and disposes
                // it at the end of the session either way. What it buys is the arm -- a source
                // implementing IEnumerable<T> more than once dispatches the non-generic read below
                // to whichever arm its type mapped that interface to, which is not the arm NU0009
                // validated, and no cast here can change that because the instance arrives as object.
                var rows = reader is not null ? reader(instance) : instance as IEnumerable;

                if (rows is not null)
                {
                    foreach (var item in rows)
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

        var repeatCount = descriptor.RepeatCount ?? 1;
        var index = 0;
        foreach (var row in allData)
        {
            var rowId = $"{idPrefix}[{index}]";

            for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                // The suffix is emitted whenever the attribute is present, [Repeat(1)] included,
                // which is what TestCaseEmitter does for every compile-time expansion. Suppressing it
                // for a count of one would make the id depend on the count rather than on the
                // attribute, and raising [Repeat(1)] to [Repeat(2)] would then rename the first
                // iteration's test case. A method with no [Repeat] keeps the bare row id it has
                // always had.
                yield return seed.CreateTestCase(
                    descriptor.RepeatCount.HasValue ? $"{rowId}#{repeatIndex}" : rowId,
                    row.Arguments,
                    index,
                    testMethod,
                    row,
                    descriptor.RepeatCount.HasValue ? repeatIndex : null);
            }

            index++;
        }
    }

}
