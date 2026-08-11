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
/// <para>
/// A descriptor with <see cref="TestDataDescriptor.DeferredEnumeration"/> is the exception: every
/// entry point here yields one placeholder test case for it instead of reading its rows, and
/// <see cref="ExpandDeferredAsync"/> is the only method that enumerates it. That asymmetry is the
/// point -- discovery must stay O(1) per deferred source however it is reached, and only the
/// execution engine is allowed to pay the enumeration cost.
/// </para>
/// </remarks>
internal static class TestDataExpander
{
    private const BindingFlags StaticMemberLookup =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

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
            if (descriptor.DeferredEnumeration)
            {
                testCases.Add(CreateDeferredPlaceholder(descriptor));
                continue;
            }

            var rows = await ResolveRowsAsync(descriptor, cancellationToken).ConfigureAwait(false);
            testCases.AddRange(ExpandRows(descriptor, rows));
        }

        return testCases;
    }

    /// <summary>
    /// Enumerates the rows of one deferred descriptor, ignoring
    /// <see cref="TestDataDescriptor.DeferredEnumeration"/>.
    /// </summary>
    /// <param name="descriptor">The descriptor a placeholder was produced for.</param>
    /// <param name="cancellationToken">The run token that cancels enumeration.</param>
    /// <returns>A task producing the test cases the placeholder stands for.</returns>
    /// <remarks>
    /// The single place a deferred source is actually read. Called by
    /// <see cref="TestExecutionEngine"/> before it builds the dependency graph, with the run
    /// cancellation token rather than a discovery one.
    /// </remarks>
    public static async ValueTask<IReadOnlyList<TestCaseDescriptor>> ExpandDeferredAsync(
        TestDataDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        // Checked before the provider runs. A source is deferred precisely because reading it is
        // expensive, so a run that was cancelled before its first test must not start reading one.
        cancellationToken.ThrowIfCancellationRequested();

        var projector = new RowProjector(descriptor);

        if (descriptor.AsyncDataSourceProvider is { } asyncProvider)
        {
            // Projected row by row instead of materialized and mapped afterwards: holding the whole
            // source as raw rows and again as test cases would put two collections proportional to
            // the source in memory at once, on the one path that exists for sources large enough for
            // that to matter.
            return await MaterializeAsync(asyncProvider, cancellationToken, projector.Project)
                .ConfigureAwait(false);
        }

        return ProjectSyncRows(projector, ResolveSyncRows(descriptor), cancellationToken);
    }

    /// <summary>
    /// Projects a synchronous deferred source into test cases, checking the run token as it goes.
    /// </summary>
    /// <remarks>
    /// The per-row check has no counterpart on the discovery paths, and deliberately so: a lazy
    /// synchronous sequence is enumerated on the calling thread, so nothing outside this loop can
    /// interrupt one that keeps yielding. During discovery that only delays startup, but here the
    /// user has already asked a running test session to stop.
    /// <para>
    /// Enumerated explicitly rather than with <c>foreach</c> so the token is checked before each
    /// <c>MoveNext</c> instead of after it. Producing the next row is the expensive half of a lazy
    /// source, and a <c>foreach</c> would always advance the producer once more before the body
    /// could observe cancellation, making the run pay for a row it will never use. Disposal is not
    /// lost with the <c>foreach</c>: the enumerator is still released by a <c>using</c>.
    /// </para>
    /// </remarks>
    private static List<TestCaseDescriptor> ProjectSyncRows(
        RowProjector projector,
        IEnumerable data,
        CancellationToken cancellationToken)
    {
        var testCases = new List<TestCaseDescriptor>();
        var index = 0;
        var enumerator = data.GetEnumerator();

        // IEnumerable.GetEnumerator returns the non-generic IEnumerator, which does not implement
        // IDisposable, so the enumerator cannot be declared with using directly. The compiler
        // generated iterators this actually receives do implement it, and using on a null-valued
        // IDisposable is a no-op, so this covers both without a hand-written finally.
        using var enumeratorDisposal = enumerator as IDisposable;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!enumerator.MoveNext())
            {
                break;
            }

            testCases.Add(projector.Project(enumerator.Current, index));
            index++;
        }

        // No check covers the MoveNext that ends the sequence -- the loop breaks on it rather than
        // coming back round -- so a token cancelled during that step would otherwise go unobserved.
        cancellationToken.ThrowIfCancellationRequested();
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
        if (descriptor.DeferredEnumeration)
        {
            yield return CreateDeferredPlaceholder(descriptor);
            yield break;
        }

        foreach (var testCase in ExpandRows(descriptor, ResolveRows(descriptor, cancellationToken)))
        {
            yield return testCase;
        }
    }

    /// <summary>
    /// Builds the single test case that represents a deferred source before execution expands it.
    /// </summary>
    /// <remarks>
    /// The identifier is the row prefix without a row index, so it is unique per <c>[TestData]</c>
    /// attribute and still maps back to the descriptor's base id by splitting on the first
    /// <c>':'</c>, which is how the VSTest adapter turns a selected test back into a descriptor.
    /// </remarks>
    private static TestCaseDescriptor CreateDeferredPlaceholder(TestDataDescriptor descriptor) =>
        new TestCaseSeed(descriptor).CreateDeferredPlaceholder(BuildRowIdPrefix(descriptor), descriptor);

    /// <summary>
    /// Builds the identifier prefix shared by every row of one data source.
    /// </summary>
    /// <remarks>
    /// The data source type and member name are part of it so that two <c>[TestData]</c> attributes
    /// naming identically named members on different types cannot collide.
    /// </remarks>
    private static string BuildRowIdPrefix(TestDataDescriptor descriptor)
    {
        var dataSourceType = descriptor.DataSourceType ?? descriptor.TestClass;
        return $"{descriptor.BaseId}:{dataSourceType.FullName}.{descriptor.DataSourceName}";
    }

    private static IEnumerable<TestCaseDescriptor> ExpandRows(TestDataDescriptor descriptor, IEnumerable data)
    {
        var projector = new RowProjector(descriptor);

        var index = 0;
        foreach (var dataRow in data)
        {
            yield return projector.Project(dataRow, index);
            index++;
        }
    }

    /// <summary>
    /// Turns one raw data row into the test case it expands to.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="ExpandRows"/> so the deferred asynchronous path can project each row
    /// as it arrives rather than materializing the whole source first. The per-descriptor state --
    /// the seed, the resolved invoker, and the id prefix -- is built once and reused for every row,
    /// which is why this is an object rather than a closure per call.
    /// </remarks>
    private sealed class RowProjector
    {
        private readonly TestCaseSeed _seed;
        private readonly TestMethodWithArgumentsDelegate? _testMethod;
        private readonly string _idPrefix;

        public RowProjector(TestDataDescriptor descriptor)
        {
            _seed = new TestCaseSeed(descriptor);
            _testMethod = _seed.ResolveTestInvoker();
            _idPrefix = BuildRowIdPrefix(descriptor);
        }

        public TestCaseDescriptor Project(object? dataRow, int index)
        {
            var row = TestDataRowResolver.Resolve(dataRow);
            return _seed.CreateTestCase($"{_idPrefix}[{index}]", row.Arguments, index, _testMethod, row);
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
                .Run(() => MaterializeAsync(asyncProvider, cancellationToken, KeepRow).AsTask())
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
            return await MaterializeAsync(asyncProvider, cancellationToken, KeepRow).ConfigureAwait(false);
        }

        return ResolveSyncRows(descriptor);
    }

    /// <summary>
    /// The identity projection used by the discovery paths, which map the rows afterwards.
    /// </summary>
    private static object? KeepRow(object? row, int index) => row;

    /// <summary>
    /// Drains an asynchronous row sequence into a list, projecting each row as it arrives.
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
    /// <para>
    /// The projection exists so the deferred path can build test cases directly and keep exactly one
    /// collection proportional to the source. The discovery paths pass <see cref="KeepRow"/> and are
    /// unaffected; routing both through one method keeps the cancellation and disposal handling
    /// above in a single place rather than duplicating it for the deferred case.
    /// </para>
    /// </remarks>
    private static async ValueTask<List<TResult>> MaterializeAsync<TResult>(
        AsyncDataSourceProviderDelegate asyncProvider,
        CancellationToken cancellationToken,
        Func<object?, int, TResult> project)
    {
        // Checked before the provider runs: invoking it starts the member's work, and a
        // task-wrapped member cannot be called back once started, so an already-cancelled request
        // must not reach it at all.
        cancellationToken.ThrowIfCancellationRequested();

        var rows = new List<TResult>();
        var index = 0;
        var enumerator = asyncProvider(cancellationToken).GetAsyncEnumerator(cancellationToken);
        var enumeratorIsDisposable = true;
        var enumerationSucceeded = false;

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

                        // Anything but a clean result may still carry a failure nobody awaits: an
                        // abandoned move faults later with no waiter left, and one that faulted as
                        // the race was lost was never propagated either. The successful case is
                        // excluded so the per-row path keeps allocating nothing extra.
                        if (!moveTask.IsCompletedSuccessfully)
                        {
                            ObserveAbandonedFailure(moveTask);
                        }
                    }
                }

                if (!hasRow)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(project(enumerator.Current, index));
                index++;
            }

            // The per-row check never runs for the move that ends the sequence, so a token cancelled
            // exactly as enumeration finished would otherwise be reported as a complete result.
            cancellationToken.ThrowIfCancellationRequested();
            enumerationSucceeded = true;
        }
        finally
        {
            if (enumeratorIsDisposable)
            {
                await DisposeEnumeratorAsync(
                    enumerator,
                    cancellationToken,
                    // Suppress the cancellation only while an exception is already unwinding.
                    // Otherwise the rows are complete and cancellation during cleanup is the only
                    // failure there is, so swallowing it would report a cancelled request as a
                    // successful discovery.
                    suppressCancellation: !enumerationSucceeded).ConfigureAwait(false);
            }
        }

        // The last word on cancellation, after cleanup rather than before it. Cleanup is itself a
        // place a token can be cancelled, and a disposal that completes successfully reports
        // nothing, so without this check the rows would come back as a complete result on a
        // cancelled request. Checking here makes the guarantee uniform: this method never returns
        // rows to a caller whose token was cancelled, wherever the cancellation happened to land.
        cancellationToken.ThrowIfCancellationRequested();
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
    /// Cancellation is swallowed only when <paramref name="suppressCancellation"/> says an exception
    /// is already unwinding, since this runs from a <c>finally</c> and rethrowing there would
    /// replace whatever actually ended the enumeration with a cancellation that says nothing about
    /// it. When enumeration succeeded, cancellation during cleanup is the only failure there is and
    /// must reach the caller: reporting a cancelled request as a successful discovery is worse than
    /// either outcome it is chosen between.
    /// </para>
    /// </remarks>
    private static async ValueTask DisposeEnumeratorAsync(
        IAsyncDisposable enumerator,
        CancellationToken cancellationToken,
        bool suppressCancellation)
    {
        try
        {
            // The call itself is inside the try, not just the await. Every step here can raise the
            // cancellation -- the invocation, a synchronously completed result, or the pending wait
            // -- and this runs from a finally, so any one of them escaping would replace the actual
            // data source failure the caller needs with a cancellation that explains nothing.
            var dispose = enumerator.DisposeAsync();

            if (dispose.IsCompleted)
            {
                dispose.GetAwaiter().GetResult();
                return;
            }

            var disposeTask = dispose.AsTask();
            try
            {
                await disposeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Cleanup abandoned by the race is the other task nobody is left holding, so it is
                // observed on the same terms as an abandoned move.
                if (!disposeTask.IsCompletedSuccessfully)
                {
                    ObserveAbandonedFailure(disposeTask);
                }
            }
        }
        catch (OperationCanceledException) when (suppressCancellation && cancellationToken.IsCancellationRequested)
        {
            // Filtered on the discovery token so only the race's own cancellation is swallowed. A
            // source that cancels its cleanup for its own reasons still surfaces the failure.
        }
    }

    /// <summary>
    /// Reads the exception of a task nothing is left to await, so it cannot resurface as an
    /// unobserved one.
    /// </summary>
    /// <remarks>
    /// A move or a disposal that lost its race against the cancellation token is walked away from
    /// deliberately -- awaiting either would reintroduce the hang the race exists to prevent -- and
    /// a source that faults afterwards then leaves a faulted task with no owner. Its finalizer
    /// raises <see cref="TaskScheduler.UnobservedTaskException"/>, which a host is free to treat as
    /// fatal, so a run that cancelled cleanly could still be killed by the source it walked away
    /// from.
    /// <para>
    /// The failure stays silent, matching how the shared discovery build in
    /// <c>NextUnitFramework</c> observes a task its waiter may have left. The caller is already
    /// being told about the cancellation it asked for, and reporting a second failure from work the
    /// run deliberately abandoned would name something nobody can act on.
    /// </para>
    /// </remarks>
    private static void ObserveAbandonedFailure(Task task) =>
        _ = task.ContinueWith(
            static failed => _ = failed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

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
    /// <para>
    /// <see cref="BindingFlags.FlattenHierarchy"/> is what makes a member declared on a base test
    /// class reachable: without it the lookup stops at <paramref name="sourceType"/>, so a source
    /// C# resolves as <c>Derived.Rows</c> was reported as missing here. It also picks the
    /// most-derived declaration when a derived type shadows the base member with <c>new</c>, which
    /// is the precedence the compile-time resolver applies.
    /// </para>
    /// <para>
    /// It does not return a base type's <c>private</c> members, so those stay unreachable here even
    /// though the resolver names them in <c>NU0020</c>. That asymmetry costs nothing: the member is
    /// out of reach of the generated registry either way, so the build fails on the diagnostic
    /// before this path can run.
    /// </para>
    /// </remarks>
    private static IEnumerable? GetTestData(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type sourceType,
        string memberName)
    {
        try
        {
            // Try to find a static method first. Selected by signature rather than by name alone:
            // with the hierarchy flattened, a base Rows() and a derived Rows(CancellationToken) are
            // both candidates for the name, which throws AmbiguousMatchException. Naming the empty
            // signature picks the parameterless overload, which is the one the compile-time resolver
            // binds and the only one this can invoke.
            var method = sourceType.GetMethod(
                memberName,
                StaticMemberLookup,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method is not null)
            {
                return method.Invoke(null, null) as IEnumerable;
            }

            // Try to find a static property
            var property = sourceType.GetProperty(memberName, StaticMemberLookup);

            if (property is not null)
            {
                return property.GetValue(null) as IEnumerable;
            }

            // Try to find a static field
            var field = sourceType.GetField(memberName, StaticMemberLookup);

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
