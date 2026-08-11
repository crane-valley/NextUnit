using System.Collections;
using System.Diagnostics.CodeAnalysis;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins which shared data source instances the two expanders hand out, and when those instances are
/// disposed.
/// </summary>
/// <remarks>
/// Every test lives in this one class on purpose. The store is process-wide, so tests that populate
/// or empty it must not run concurrently, and xUnit runs the methods of a single class sequentially.
/// Each test also uses its own marker type, so one test only ever counts its own instances.
/// <para>
/// Through 1.x each expander kept its own caches: the first two assertions below produced two and
/// three instances respectively, because the same type reached through the other attribute was
/// instantiated again.
/// </para>
/// </remarks>
public sealed class SharedInstanceStoreTests
{
    private static readonly List<string> _log = [];

    [Fact]
    public void SharedInstance_PerSessionThroughBothAttributes_IsCreatedOnce()
    {
        ExpandClassDataSource(typeof(RecordingSource<CrossAttributeScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandValuesFrom(typeof(RecordingSource<CrossAttributeScope>), SharedType.PerSession, typeof(FirstTestClass));

        Assert.Equal(1, CreatedCount<CrossAttributeScope>());
    }

    [Fact]
    public void SharedInstance_PerSessionThroughOneAttribute_IsCreatedOnce()
    {
        ExpandClassDataSource(typeof(RecordingSource<WithinAttributeScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(RecordingSource<WithinAttributeScope>), SharedType.PerSession, typeof(SecondTestClass));

        Assert.Equal(1, CreatedCount<WithinAttributeScope>());
    }

    [Fact]
    public void SharedInstance_PerClass_IsCreatedOncePerTestClass()
    {
        ExpandClassDataSource(typeof(RecordingSource<PerClassScope>), SharedType.PerClass, typeof(FirstTestClass));
        ExpandValuesFrom(typeof(RecordingSource<PerClassScope>), SharedType.PerClass, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(RecordingSource<PerClassScope>), SharedType.PerClass, typeof(SecondTestClass));

        Assert.Equal(2, CreatedCount<PerClassScope>());
    }

    [Fact]
    public void SharedInstance_Keyed_IsCreatedOncePerKey()
    {
        ExpandClassDataSource(typeof(RecordingSource<KeyedScope>), SharedType.Keyed, typeof(FirstTestClass), key: "left");
        ExpandValuesFrom(typeof(RecordingSource<KeyedScope>), SharedType.Keyed, typeof(SecondTestClass), key: "left");
        ExpandClassDataSource(typeof(RecordingSource<KeyedScope>), SharedType.Keyed, typeof(FirstTestClass), key: "right");

        Assert.Equal(2, CreatedCount<KeyedScope>());
    }

    [Fact]
    public void SharedInstance_None_IsCreatedForEveryExpansion()
    {
        ExpandClassDataSource(typeof(RecordingSource<UnsharedScope>), SharedType.None, typeof(FirstTestClass));
        ExpandValuesFrom(typeof(RecordingSource<UnsharedScope>), SharedType.None, typeof(FirstTestClass));

        Assert.Equal(2, CreatedCount<UnsharedScope>());
    }

    [Fact]
    public void SharedInstance_PerAssemblyAndPerSession_StayDistinct()
    {
        ExpandClassDataSource(typeof(RecordingSource<ScopeSeparationScope>), SharedType.PerAssembly, typeof(FirstTestClass));
        ExpandValuesFrom(typeof(RecordingSource<ScopeSeparationScope>), SharedType.PerSession, typeof(FirstTestClass));

        // The two scopes coincide in a single-assembly run, but they are documented as different
        // lifetimes, so unifying the store must not collapse them onto one instance.
        Assert.Equal(2, CreatedCount<ScopeSeparationScope>());
    }

    [Fact]
    public void SharedInstance_PerAssembly_IsCreatedOncePerTestAssembly()
    {
        // Assert stands in for a test class in another assembly, which is all the scope keys on.
        ExpandClassDataSource(typeof(RecordingSource<AssemblyScope>), SharedType.PerAssembly, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(RecordingSource<AssemblyScope>), SharedType.PerAssembly, typeof(SecondTestClass));
        ExpandClassDataSource(typeof(RecordingSource<AssemblyScope>), SharedType.PerAssembly, typeof(Assert));

        Assert.Equal(2, CreatedCount<AssemblyScope>());
    }

    [Fact]
    public void SharedInstance_PerSession_SpansTestAssemblies()
    {
        ExpandClassDataSource(typeof(RecordingSource<SessionScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(RecordingSource<SessionScope>), SharedType.PerSession, typeof(Assert));

        // The one thing PerSession does that PerAssembly does not.
        Assert.Equal(1, CreatedCount<SessionScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_DisposesEveryInstanceAndEmptiesTheStoreAsync()
    {
        ExpandClassDataSource(typeof(RecordingSource<DisposalScope>), SharedType.PerSession, typeof(FirstTestClass));

        await SharedInstanceStore.DisposeAllAsync();

        Assert.Equal(new[] { "created", "disposed" }, Entries<DisposalScope>());

        // The store is emptied as well as disposed, so a later expansion cannot be handed an instance
        // that was already released.
        ExpandClassDataSource(typeof(RecordingSource<DisposalScope>), SharedType.PerSession, typeof(FirstTestClass));

        Assert.Equal(2, CreatedCount<DisposalScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_LeavesAnUnsharedInstanceAloneAsync()
    {
        ExpandClassDataSource(typeof(RecordingSource<UnsharedDisposalScope>), SharedType.None, typeof(FirstTestClass));

        await SharedInstanceStore.DisposeAllAsync();

        // SharedType.None never enters the store: the instance belongs to the single expansion that
        // asked for it, and disposing it here would be disposing something the store never owned.
        Assert.Equal(new[] { "created" }, Entries<UnsharedDisposalScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_PrefersAsynchronousDisposalAsync()
    {
        ExpandClassDataSource(typeof(DualDisposableSource<DualDisposalScope>), SharedType.PerSession, typeof(FirstTestClass));

        await SharedInstanceStore.DisposeAllAsync();

        Assert.Equal(new[] { "created", "disposed-async" }, Entries<DualDisposalScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_DisposesAnAsynchronousOnlyInstanceAsync()
    {
        ExpandValuesFrom(typeof(AsyncDisposableSource<AsyncDisposalScope>), SharedType.PerSession, typeof(FirstTestClass));

        await SharedInstanceStore.DisposeAllAsync();

        Assert.Equal(new[] { "created", "disposed-async" }, Entries<AsyncDisposalScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_DisposesInReverseCreationOrderAsync()
    {
        ExpandClassDataSource(typeof(RecordingSource<FirstCreatedScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(RecordingSource<SecondCreatedScope>), SharedType.PerSession, typeof(FirstTestClass));

        await SharedInstanceStore.DisposeAllAsync();

        Assert.Equal(
            new[] { $"{nameof(SecondCreatedScope)}:disposed", $"{nameof(FirstCreatedScope)}:disposed" },
            DisposalOrder(nameof(FirstCreatedScope), nameof(SecondCreatedScope)));
    }

    [Fact]
    public async Task DisposeAllAsync_ReportsAFailureAndStillDisposesTheRestAsync()
    {
        ExpandClassDataSource(typeof(RecordingSource<SurvivingScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(FailingDisposableSource<FailingScope>), SharedType.PerSession, typeof(FirstTestClass));

        // Reverse creation order disposes the failing instance first, so the surviving one proves that
        // a failure does not abandon the instances behind it.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SharedInstanceStore.DisposeAllAsync().AsTask());

        Assert.Equal("dispose boom", exception.Message);
        Assert.Equal(new[] { "created", "disposed" }, Entries<SurvivingScope>());
    }

    [Fact]
    public void CriticalFailure_IsFoundThroughEveryWrapperADisposerCanUse()
    {
        // Constructed, never thrown: these stand for the trees a disposer can hand a cleanup path.
        Assert.True(ExceptionHelper.IsCriticalFailure(new OutOfMemoryException()));
        Assert.True(ExceptionHelper.IsCriticalFailure(new AggregateException(new OutOfMemoryException())));
        Assert.True(ExceptionHelper.IsCriticalFailure(
            new AggregateException(new AggregateException(new AccessViolationException()))));
        Assert.True(ExceptionHelper.IsCriticalFailure(
            new InvalidOperationException("wrapped", new OutOfMemoryException())));
        Assert.True(ExceptionHelper.IsCriticalFailure(
            new AggregateException(new InvalidOperationException("first"), new OutOfMemoryException())));

        Assert.False(ExceptionHelper.IsCriticalFailure(null));
        Assert.False(ExceptionHelper.IsCriticalFailure(new InvalidOperationException("ordinary")));
        Assert.False(ExceptionHelper.IsCriticalFailure(
            new AggregateException(new InvalidOperationException("first"), new InvalidOperationException("second"))));

        // No depth at which the answer flips back to "ordinary": a bound would have to guess, and
        // guessing wrong here is what swallows the failure.
        Exception deeplyWrapped = new OutOfMemoryException();
        for (var depth = 0; depth < 64; depth++)
        {
            deeplyWrapped = new InvalidOperationException($"layer {depth}", deeplyWrapped);
        }

        Assert.True(ExceptionHelper.IsCriticalFailure(deeplyWrapped));
    }

    [Fact]
    public async Task DisposeAllAsync_LetsACriticalFailureInsideAnAggregateEscapeAsync()
    {
        // Reverse creation order disposes the critical one first, so whether the ordinary one is
        // disposed after it is what separates escaping from being collected as a failure: filed as an
        // ordinary disposal failure, this would be reported as one badly behaved data source while
        // the process is actually out of memory, and the loop would carry on.
        ExpandClassDataSource(typeof(RecordingSource<UntouchedScope>), SharedType.PerSession, typeof(FirstTestClass));
        ExpandClassDataSource(typeof(CriticallyFailingSource<CriticalScope>), SharedType.PerSession, typeof(FirstTestClass));

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => SharedInstanceStore.DisposeAllAsync().AsTask());

        // The disposer's own aggregate, not the store's "one or more instances failed" wrapper.
        Assert.True(exception.Message.StartsWith("cleanup failed", StringComparison.Ordinal));
        Assert.True(exception.InnerExceptions.Any(inner => inner is OutOfMemoryException));
        Assert.Equal(new[] { "created" }, Entries<UntouchedScope>());
    }

    [Fact]
    public async Task DisposeAllAsync_StillOwnsAnInstanceCreatedWhileItWasRunningAsync()
    {
        // The constructor empties the store from inside its own creation, which is the worst case a
        // cleanup interleaving with a data source constructor can produce. Neither host does this,
        // but the store's own state has to survive it: the instance must end up owned rather than
        // orphaned, and must not be disposed before the caller that asked for it sees it.
        ExpandClassDataSource(typeof(SelfDisposingStoreSource<RacingScope>), SharedType.PerSession, typeof(FirstTestClass));

        Assert.Equal(new[] { "created" }, Entries<RacingScope>());

        await SharedInstanceStore.DisposeAllAsync();

        Assert.Equal(new[] { "created", "disposed" }, Entries<RacingScope>());
    }

    private static void ExpandClassDataSource(
        Type sourceType,
        SharedType shared,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type testClass,
        string? key = null)
    {
        var descriptor = new ClassDataSourceDescriptor
        {
            BaseId = $"Tests.{sourceType.Name}.Class",
            TestClass = testClass,
            MethodName = nameof(FirstTestClass.Run),
            DataSourceTypes = [sourceType],
            ParameterTypes = [typeof(int)],
            SharedType = shared,
            SharedKey = key
        };

        Assert.NotEmpty(ClassDataSourceExpander.ExpandSingle(descriptor).ToList());
    }

    private static void ExpandValuesFrom(
        Type sourceType,
        SharedType shared,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type testClass,
        string? key = null)
    {
        var descriptor = new CombinedDataSourceDescriptor
        {
            BaseId = $"Tests.{sourceType.Name}.Combined",
            TestClass = testClass,
            MethodName = nameof(FirstTestClass.Run),
            ParameterTypes = [typeof(int)],
            ParameterSources =
            [
                new ParameterDataSource
                {
                    ParameterIndex = 0,
                    ParameterName = "value",
                    Kind = ParameterDataSourceKind.Class,
                    ClassDataSourceType = sourceType,
                    SharedType = shared,
                    SharedKey = key
                }
            ]
        };

        Assert.NotEmpty(CombinedDataSourceExpander.ExpandSingle(descriptor).ToList());
    }

    private static void Record(Type scope, string entry)
    {
        lock (_log)
        {
            _log.Add($"{scope.Name}:{entry}");
        }
    }

    /// <summary>
    /// Returns what one marker type recorded, with the marker name stripped off.
    /// </summary>
    private static string[] Entries<TScope>()
    {
        var prefix = $"{typeof(TScope).Name}:";

        lock (_log)
        {
            return _log
                .Where(entry => entry.StartsWith(prefix, StringComparison.Ordinal))
                .Select(entry => entry.Substring(prefix.Length))
                .ToArray();
        }
    }

    private static int CreatedCount<TScope>() =>
        Entries<TScope>().Count(entry => entry == "created");

    /// <summary>
    /// Returns the disposals recorded by the named marker types, in the order they happened.
    /// </summary>
    private static string[] DisposalOrder(params string[] scopeNames)
    {
        lock (_log)
        {
            return _log
                .Where(entry => entry.EndsWith(":disposed", StringComparison.Ordinal)
                    && scopeNames.Contains(entry.Split(':')[0], StringComparer.Ordinal))
                .ToArray();
        }
    }

    private sealed class CrossAttributeScope;

    private sealed class WithinAttributeScope;

    private sealed class PerClassScope;

    private sealed class KeyedScope;

    private sealed class UnsharedScope;

    private sealed class ScopeSeparationScope;

    private sealed class DisposalScope;

    private sealed class UnsharedDisposalScope;

    private sealed class DualDisposalScope;

    private sealed class AsyncDisposalScope;

    private sealed class FirstCreatedScope;

    private sealed class SecondCreatedScope;

    private sealed class SurvivingScope;

    private sealed class FailingScope;

    private sealed class RacingScope;

    private sealed class AssemblyScope;

    private sealed class SessionScope;

    private sealed class CriticalScope;

    private sealed class UntouchedScope;

    private sealed class FirstTestClass
    {
        public void Run(int value) => _ = value;
    }

    private sealed class SecondTestClass
    {
        public void Run(int value) => _ = value;
    }

    private abstract class SingleRowSource : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [1];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingSource<TScope> : SingleRowSource, IDisposable
    {
        public RecordingSource() => Record(typeof(TScope), "created");

        public void Dispose() => Record(typeof(TScope), "disposed");
    }

    private sealed class AsyncDisposableSource<TScope> : SingleRowSource, IAsyncDisposable
    {
        public AsyncDisposableSource() => Record(typeof(TScope), "created");

        public ValueTask DisposeAsync()
        {
            Record(typeof(TScope), "disposed-async");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DualDisposableSource<TScope> : SingleRowSource, IDisposable, IAsyncDisposable
    {
        public DualDisposableSource() => Record(typeof(TScope), "created");

        public void Dispose() => Record(typeof(TScope), "disposed");

        public ValueTask DisposeAsync()
        {
            Record(typeof(TScope), "disposed-async");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingDisposableSource<TScope> : SingleRowSource, IDisposable
    {
        public FailingDisposableSource() => Record(typeof(TScope), "created");

        public void Dispose()
        {
            Record(typeof(TScope), "disposed");
            throw new InvalidOperationException("dispose boom");
        }
    }

    /// <summary>
    /// A disposer that reports a critical failure the way a real one would: wrapped in whatever it
    /// caught rather than thrown bare.
    /// </summary>
    private sealed class CriticallyFailingSource<TScope> : SingleRowSource, IDisposable
    {
        public CriticallyFailingSource() => Record(typeof(TScope), "created");

        public void Dispose() =>
            throw new AggregateException("cleanup failed", new OutOfMemoryException());
    }

    /// <summary>
    /// Stands in for a cleanup that runs while this constructor is still going, by emptying the store
    /// from inside the constructor itself.
    /// </summary>
    private sealed class SelfDisposingStoreSource<TScope> : SingleRowSource, IDisposable
    {
        public SelfDisposingStoreSource()
        {
            SharedInstanceStore.DisposeAll();
            Record(typeof(TScope), "created");
        }

        public void Dispose() => Record(typeof(TScope), "disposed");
    }
}
