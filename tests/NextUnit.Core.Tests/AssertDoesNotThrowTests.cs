namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.DoesNotThrow and Assert.DoesNotThrowAsync.
/// </summary>
public class AssertDoesNotThrowTests
{
    [Test]
    public void DoesNotThrow_ActionSucceeds_DoesNotThrow()
    {
        var ran = false;
        Assert.DoesNotThrow(() => ran = true);
        Assert.True(ran);
    }

    [Test]
    public void DoesNotThrow_ActionThrows_ThrowsWithExceptionDetails()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotThrow(() => throw new InvalidOperationException("boom")));
        Assert.Contains("InvalidOperationException", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Test]
    public void DoesNotThrow_ActionThrows_PreservesInnerException()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotThrow(() => throw new InvalidOperationException("boom")));
        Assert.NotNull(ex.InnerException);
        Assert.Same(typeof(InvalidOperationException), ex.InnerException!.GetType());
    }

    [Test]
    public void DoesNotThrow_ActionThrows_UsesCustomMessage()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotThrow(() => throw new InvalidOperationException("boom"), "no throw expected"));
        Assert.Equal("no throw expected", ex.Message);
    }

    [Test]
    public void DoesNotThrow_NullAction_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Assert.DoesNotThrow(null!));
    }

    [Test]
    public void DoesNotThrow_ActionCallsSkip_PropagatesSkipNotFailure()
    {
        // A runtime skip must reach the engine as a skip, not be reported as a failure.
        Assert.Throws<TestSkippedException>(
            () => Assert.DoesNotThrow(() => Assert.Skip("conditionally skipped")));
    }

    [Test]
    public void DoesNotThrow_ActionThrowsCancellation_PropagatesNotFailure()
    {
        // The engine derives timeout results from OperationCanceledException, so it must
        // propagate rather than be wrapped in an assertion failure.
        Assert.Throws<OperationCanceledException>(
            () => Assert.DoesNotThrow(() => throw new OperationCanceledException()));
    }

    [Test]
    public void DoesNotThrow_InnerAssertionFails_PreservesOriginalMessage()
    {
        // An assertion failure inside the action must surface with its original formatted
        // message, not be double-wrapped behind "Expected no exception but got ...".
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotThrow(() => Assert.Equal("abc", "abd")));
        Assert.Contains("String assertion failed", ex.Message);
        Assert.False(ex.Message.Contains("Expected no exception", StringComparison.Ordinal));
        Assert.Null(ex.InnerException);
    }

    [Test]
    public void DoesNotThrow_ActionThrowsCritical_PropagatesUnwrapped()
    {
        // Critical fail-fast exceptions must not be wrapped, mirroring the engine.
        Assert.Throws<OutOfMemoryException>(
            () => Assert.DoesNotThrow(() => throw new OutOfMemoryException()));
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionSucceeds_DoesNotThrowAsync()
    {
        await Assert.DoesNotThrowAsync(() => Task.CompletedTask);
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionThrows_ThrowsWithExceptionDetailsAsync()
    {
        var ex = await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async boom");
            }));
        Assert.Contains("InvalidOperationException", ex.Message);
        Assert.Contains("async boom", ex.Message);
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionThrows_UsesCustomMessageAsync()
    {
        var ex = await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.DoesNotThrowAsync(
                () => throw new InvalidOperationException("async boom"), "no async throw"));
        Assert.Equal("no async throw", ex.Message);
    }

    [Test]
    public async Task DoesNotThrowAsync_NullAction_ThrowsArgumentNullAsync()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Assert.DoesNotThrowAsync(null!));
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionCallsSkip_PropagatesSkipNotFailureAsync()
    {
        await Assert.ThrowsAsync<TestSkippedException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                Assert.Skip("conditionally skipped");
            }));
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionThrowsCancellation_PropagatesNotFailureAsync()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                throw new OperationCanceledException();
            }));
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionThrowsTaskCanceled_PropagatesNotFailureAsync()
    {
        // TaskCanceledException derives from OperationCanceledException, so the OCE
        // rethrow covers it without a separate catch.
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                throw new TaskCanceledException();
            }));
    }

    [Test]
    public async Task DoesNotThrowAsync_InnerAssertionFails_PreservesOriginalMessageAsync()
    {
        var ex = await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                Assert.Equal("abc", "abd");
            }));
        Assert.Contains("String assertion failed", ex.Message);
        Assert.False(ex.Message.Contains("Expected no exception", StringComparison.Ordinal));
        Assert.Null(ex.InnerException);
    }

    [Test]
    public async Task DoesNotThrowAsync_ActionThrowsCritical_PropagatesUnwrappedAsync()
    {
        await Assert.ThrowsAsync<OutOfMemoryException>(
            () => Assert.DoesNotThrowAsync(async () =>
            {
                await Task.Yield();
                throw new OutOfMemoryException();
            }));
    }
}
