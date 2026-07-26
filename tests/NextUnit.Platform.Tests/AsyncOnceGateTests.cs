namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins the once-only semantics that <see cref="SessionLifecycleRunner"/> relies on for session
/// setup: the hooks run once, every caller observes them as finished, and a failed run is retried.
/// </summary>
public sealed class AsyncOnceGateTests
{
    [Test]
    public async Task RunOnceAsync_RunsTheOperationOnceForConcurrentCallersAsync()
    {
        var gate = new AsyncOnceGate();
        var starts = 0;
        var release = new TaskCompletionSource();

        Task Operation(CancellationToken _)
        {
            Interlocked.Increment(ref starts);
            return release.Task;
        }

        var callers = Enumerable
            .Range(0, 8)
            .Select(_ => Task.Run(() => gate.RunOnceAsync(Operation, CancellationToken.None)))
            .ToArray();

        // Nothing may complete while the operation is still in flight: a gate that published
        // completion before awaiting would let the other callers through here.
        await WaitUntilAsync(() => Volatile.Read(ref starts) == 1);
        Assert.False(callers.Any(static c => c.IsCompleted));
        Assert.False(gate.HasCompleted);

        release.SetResult();
        await Task.WhenAll(callers);

        Assert.Equal(1, starts);
        Assert.True(gate.HasCompleted);
    }

    [Test]
    public async Task RunOnceAsync_SkipsTheOperationAfterItSucceededAsync()
    {
        var gate = new AsyncOnceGate();
        var runs = 0;

        Task Operation(CancellationToken _)
        {
            runs++;
            return Task.CompletedTask;
        }

        await gate.RunOnceAsync(Operation, CancellationToken.None);
        await gate.RunOnceAsync(Operation, CancellationToken.None);

        Assert.Equal(1, runs);
    }

    [Test]
    public async Task RunOnceAsync_RetriesAfterAFailedOperationAsync()
    {
        var gate = new AsyncOnceGate();
        var runs = 0;

        Task Operation(CancellationToken _)
        {
            runs++;
            return runs == 1
                ? Task.FromException(new InvalidOperationException("session setup boom"))
                : Task.CompletedTask;
        }

        // A failure must not be recorded as completion, otherwise a later caller would silently skip
        // setup that never ran.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.RunOnceAsync(Operation, CancellationToken.None));
        Assert.False(gate.HasCompleted);

        await gate.RunOnceAsync(Operation, CancellationToken.None);

        Assert.Equal(2, runs);
        Assert.True(gate.HasCompleted);
    }

    [Test]
    public async Task RunOnceAsync_RetriesAfterACancelledOperationAsync()
    {
        var gate = new AsyncOnceGate();
        var runs = 0;

        // The operation observes cancellation itself, which is how a session hook aborts; the gate's
        // own token stays live so the throw comes from the operation, not from the wait.
        Task Operation(CancellationToken _)
        {
            runs++;
            return runs == 1
                ? Task.FromException(new OperationCanceledException())
                : Task.CompletedTask;
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => gate.RunOnceAsync(Operation, CancellationToken.None));
        Assert.False(gate.HasCompleted);

        await gate.RunOnceAsync(Operation, CancellationToken.None);

        Assert.Equal(2, runs);
        Assert.True(gate.HasCompleted);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition was not reached in time.");
    }
}
