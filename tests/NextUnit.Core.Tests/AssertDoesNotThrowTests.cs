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
}
