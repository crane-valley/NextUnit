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

    /// <summary>
    /// Drains an asynchronous row sequence into a list.
    /// </summary>
    /// <remarks>
    /// Each pending move is raced against the token instead of simply awaited. Handing a token to
    /// a source does not make it observe one, and forwarding a token cancels neither a
    /// <c>MoveNextAsync</c> that is already in flight nor the member call behind it, so a plain
    /// <c>await foreach</c> would let a source that never yields hang discovery with no way out.
    /// This is the outermost consumer of the row sequence, so racing here also covers whatever the
    /// provider delegate wraps.
    /// <para>
    /// A move abandoned this way leaves the enumerator mid-operation, so it is deliberately not
    /// disposed: awaiting <c>DisposeAsync</c> would block on the very operation the race just
    /// walked away from, reintroducing the hang the race exists to prevent.
    /// </para>
    /// </remarks>
    private static async ValueTask<IReadOnlyList<object?>> MaterializeAsync(
        AsyncDataSourceProviderDelegate asyncProvider,
        CancellationToken cancellationToken)
    {
        // Checked before the provider runs: invoking it starts the member's work, and a
        // task-wrapped member cannot be called back once started, so an already-cancelled request
        // must not reach it at all.
        cancellationToken.ThrowIfCancellationRequested();

        var rows = new List<object?>();
        var enumerator = asyncProvider(cancellationToken).GetAsyncEnumerator(cancellationToken);
        var enumeratorIsDisposable = true;

        try
        {
            while (true)
            {
                var move = enumerator.MoveNextAsync();

                bool hasRow;
                if (move.IsCompleted)
                {
                    // The synchronous completion path, which is the common one: taking it avoids
                    // allocating a Task per row.
                    hasRow = move.GetAwaiter().GetResult();
                }
                else
                {
                    var moveTask = move.AsTask();
                    try
                    {
                        hasRow = await moveTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        // Only an abandoned move leaves the enumerator unusable. One that
                        // completed, successfully or not, can still be disposed normally.
                        enumeratorIsDisposable = moveTask.IsCompleted;
                    }
                }

                if (!hasRow)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(enumerator.Current);
            }
        }
        finally
        {
            if (enumeratorIsDisposable)
            {
                await DisposeEnumeratorAsync(enumerator, cancellationToken).ConfigureAwait(false);
            }
        }

        return rows;
    }

    /// <summary>
    /// Disposes a row enumerator without letting cleanup outlast a cancelled request.
    /// </summary>
    /// <remarks>
    /// A source is free to await non-cancellable cleanup in <c>DisposeAsync</c>, which would hang
    /// discovery just as surely as a move that never completes, so a pending disposal is raced
    /// against the token too. Disposal that completes synchronously, which is the usual case for a
    /// compiler-generated iterator, never reaches the race. Abandoning cleanup can leak whatever
    /// the source held; that is the accepted cost of guaranteeing that cancellation returns.
    /// <para>
    /// Cancellation is swallowed rather than rethrown because this runs from a <c>finally</c>:
    /// rethrowing would replace whatever actually ended the enumeration with a cancellation that
    /// says nothing about it.
    /// </para>
    /// </remarks>
    private static async ValueTask DisposeEnumeratorAsync(
        IAsyncDisposable enumerator,
        CancellationToken cancellationToken)
    {
        var dispose = enumerator.DisposeAsync();
        if (dispose.IsCompleted)
        {
            dispose.GetAwaiter().GetResult();
            return;
        }

        try
        {
            await dispose.AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
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
