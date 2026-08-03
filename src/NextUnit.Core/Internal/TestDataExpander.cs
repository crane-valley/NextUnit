using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="TestDataDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by invoking data source members at runtime.
/// </summary>
/// <remarks>
/// Rows of an asynchronous source are materialized during discovery exactly as synchronous rows
/// are, so both kinds of source produce the same observable set of test cases and stay individually
/// selectable and filterable in an IDE.
/// </remarks>
public static class TestDataExpander
{
    /// <summary>
    /// Expands a collection of test data descriptors into test case descriptors.
    /// </summary>
    /// <param name="testDataDescriptors">The test data descriptors to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(IEnumerable<TestDataDescriptor> testDataDescriptors) =>
        Expand(testDataDescriptors, CancellationToken.None);

    /// <summary>
    /// Expands a collection of test data descriptors into test case descriptors.
    /// </summary>
    /// <param name="testDataDescriptors">The test data descriptors to expand.</param>
    /// <param name="cancellationToken">The token that cancels enumeration of an asynchronous source.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    /// <remarks>
    /// Blocks while draining an asynchronous source. Callers that already have an asynchronous
    /// context should use <see cref="ExpandAsync"/> instead.
    /// </remarks>
    public static IEnumerable<TestCaseDescriptor> Expand(
        IEnumerable<TestDataDescriptor> testDataDescriptors,
        CancellationToken cancellationToken)
    {
        foreach (var descriptor in testDataDescriptors)
        {
            foreach (var testCase in ExpandSingle(descriptor, cancellationToken))
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Expands a collection of test data descriptors into test case descriptors, awaiting
    /// asynchronous data source members instead of blocking on them.
    /// </summary>
    /// <param name="testDataDescriptors">The test data descriptors to expand.</param>
    /// <param name="cancellationToken">The token that cancels enumeration of an asynchronous source.</param>
    /// <returns>A task producing the expanded test case descriptors.</returns>
    public static async ValueTask<IReadOnlyList<TestCaseDescriptor>> ExpandAsync(
        IEnumerable<TestDataDescriptor> testDataDescriptors,
        CancellationToken cancellationToken)
    {
        var testCases = new List<TestCaseDescriptor>();

        foreach (var descriptor in testDataDescriptors)
        {
            var rows = await ResolveRowsAsync(descriptor, cancellationToken).ConfigureAwait(false);
            testCases.AddRange(ExpandRows(descriptor, rows));
        }

        return testCases;
    }

    /// <summary>
    /// Expands a single test data descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The test data descriptor to expand.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(TestDataDescriptor descriptor) =>
        ExpandSingle(descriptor, CancellationToken.None);

    /// <summary>
    /// Expands a single test data descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The test data descriptor to expand.</param>
    /// <param name="cancellationToken">The token that cancels enumeration of an asynchronous source.</param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(
        TestDataDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        foreach (var testCase in ExpandRows(descriptor, ResolveRows(descriptor, cancellationToken)))
        {
            yield return testCase;
        }
    }

    private static IEnumerable<TestCaseDescriptor> ExpandRows(TestDataDescriptor descriptor, IEnumerable data)
    {
        var dataSourceType = descriptor.DataSourceType ?? descriptor.TestClass;
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

    /// <summary>
    /// Resolves the rows of one descriptor, blocking when the source is asynchronous.
    /// </summary>
    /// <remarks>
    /// The blocking wait is a deliberate boundary, not an oversight. <c>ITestDiscoverer</c> and
    /// <c>ITestExecutor</c> in the VSTest adapter are synchronous contracts, and that adapter
    /// already blocks on the execution engine for the same reason. Running the drain through
    /// <see cref="Task.Run(Func{Task})"/> detaches it from any ambient synchronization context, so
    /// nothing the source awaits can post a continuation back to the thread being blocked here.
    /// Microsoft.Testing.Platform never reaches this branch: it calls <see cref="ExpandAsync"/>.
    /// </remarks>
    private static IEnumerable ResolveRows(TestDataDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.AsyncDataSourceProvider is { } asyncProvider)
        {
            return Task
                .Run(() => MaterializeAsync(asyncProvider, cancellationToken).AsTask())
                .GetAwaiter()
                .GetResult();
        }

        return ResolveSyncRows(descriptor);
    }

    private static async ValueTask<IEnumerable> ResolveRowsAsync(
        TestDataDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (descriptor.AsyncDataSourceProvider is { } asyncProvider)
        {
            return await MaterializeAsync(asyncProvider, cancellationToken).ConfigureAwait(false);
        }

        return ResolveSyncRows(descriptor);
    }

    private static async ValueTask<IReadOnlyList<object?>> MaterializeAsync(
        AsyncDataSourceProviderDelegate asyncProvider,
        CancellationToken cancellationToken)
    {
        var rows = new List<object?>();

        await foreach (var row in asyncProvider(cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static IEnumerable ResolveSyncRows(TestDataDescriptor descriptor)
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

        return data;
    }

    /// <summary>
    /// Reflection fallback for a member the source generator could not bind.
    /// </summary>
    /// <remarks>
    /// Synchronous only, deliberately. Reading an <c>IAsyncEnumerable&lt;T&gt;</c> or unwrapping a
    /// <c>Task&lt;T&gt;</c> reflectively needs runtime generic instantiation that neither trimming
    /// nor Native AOT can see, which would trade the framework's central guarantee for a path that
    /// is unreachable in practice: the generator binds every static member it can see, and a member
    /// it cannot see fails here for the same reason whether it is synchronous or asynchronous.
    /// The <c>NU0014</c> analyzer rule reports the statically detectable cases at build time.
    /// </remarks>
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
