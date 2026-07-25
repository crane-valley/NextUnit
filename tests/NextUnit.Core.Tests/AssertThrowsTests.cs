namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for Assert.Throws and Assert.ThrowsAsync, covering the correct
/// exception type, wrong type, no exception, derived-exception matching, and the
/// expected-message overloads.
/// </summary>
public class AssertThrowsTests
{
    [Test]
    public void Throws_ExpectedException_ReturnsException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => throw new InvalidOperationException("boom"));
        Assert.Equal("boom", ex.Message);
    }

    [Test]
    public void Throws_DerivedException_MatchesBaseType()
    {
        // catch (TException) matches derived exceptions, so expecting the base type succeeds.
        var ex = Assert.Throws<ArgumentException>(
            () => throw new ArgumentNullException("param"));
        Assert.NotNull(ex);
    }

    [Test]
    public void Throws_WrongExceptionType_ThrowsWithBothTypeNames()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Throws<InvalidOperationException>(
                () => throw new ArgumentException("wrong")));
        Assert.Contains("InvalidOperationException", ex.Message);
        Assert.Contains("ArgumentException", ex.Message);
    }

    [Test]
    public void Throws_BaseThrownWhenDerivedExpected_Throws()
    {
        // Expecting the derived type but the base type is thrown does not match.
        Assert.Throws<AssertionFailedException>(
            () => Assert.Throws<ArgumentNullException>(
                () => throw new ArgumentException("base")));
    }

    [Test]
    public void Throws_NoException_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Throws<InvalidOperationException>(() => { }));
        Assert.Contains("no exception was thrown", ex.Message);
    }

    [Test]
    public void Throws_TwoArgStringOverload_TreatsStringAsCustomMessageNotExpectedMessage()
    {
        // Overload resolution binds Throws(action, string) to the (Action, string? message)
        // overload, so the string is a custom failure message, not an expected-message check.
        // The mismatched second argument is therefore ignored when the exception type matches.
        var ex = Assert.Throws<InvalidOperationException>(
            () => throw new InvalidOperationException("actual"), "totally different");
        Assert.Equal("actual", ex.Message);
    }

    [Test]
    public void Throws_WithExpectedMessage_MatchingMessage_ReturnsException()
    {
        // Three arguments are required to reach the expected-message validation overload.
        var ex = Assert.Throws<InvalidOperationException>(
            () => throw new InvalidOperationException("exact"), "exact", null);
        Assert.Equal("exact", ex.Message);
    }

    [Test]
    public void Throws_WithExpectedMessage_MismatchedMessage_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Throws<InvalidOperationException>(
                () => throw new InvalidOperationException("actual"), "expected", null));
        Assert.Contains("expected", ex.Message);
        Assert.Contains("actual", ex.Message);
    }

    [Test]
    public void Throws_WithNullExpectedMessage_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => Assert.Throws<InvalidOperationException>(() => { }, (string)null!, null));
    }

    [Test]
    public async Task ThrowsAsync_ExpectedException_ReturnsExceptionAsync()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => throw new InvalidOperationException("async boom"));
        Assert.Equal("async boom", ex.Message);
    }

    [Test]
    public async Task ThrowsAsync_ExceptionAfterAwait_ReturnsExceptionAsync()
    {
        // Guards against a regression where the returned task is invoked but not awaited.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async fault");
        });
        Assert.Equal("async fault", ex.Message);
    }

    [Test]
    public async Task ThrowsAsync_WrongExceptionType_ThrowsAsync()
    {
        await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.ThrowsAsync<InvalidOperationException>(
                () => throw new ArgumentException("wrong")));
    }

    [Test]
    public async Task ThrowsAsync_NoException_ThrowsAsync()
    {
        var ex = await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.ThrowsAsync<InvalidOperationException>(() => Task.CompletedTask));
        Assert.Contains("no exception was thrown", ex.Message);
    }

    [Test]
    public async Task ThrowsAsync_WithExpectedMessage_MatchingMessage_ReturnsExceptionAsync()
    {
        // Three arguments are required to reach the expected-message validation overload.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => throw new InvalidOperationException("exact async"), "exact async", null);
        Assert.Equal("exact async", ex.Message);
    }

    [Test]
    public async Task ThrowsAsync_WithExpectedMessage_MismatchedMessage_ThrowsAsync()
    {
        await Assert.ThrowsAsync<AssertionFailedException>(
            () => Assert.ThrowsAsync<InvalidOperationException>(
                () => throw new InvalidOperationException("actual async"), "expected async", null));
    }

    [Test]
    public void Throws_NullAction_ThrowsArgumentNull()
    {
        // A missing delegate is caller misuse, so it must not be reported as a failed assertion.
        var ex = Assert.Throws<ArgumentNullException>(
            () => Assert.Throws<InvalidOperationException>((Action)null!));
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public void Throws_WithExpectedMessage_NullAction_ThrowsArgumentNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => Assert.Throws<InvalidOperationException>((Action)null!, "expected", null));
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public async Task ThrowsAsync_NullAction_ThrowsArgumentNullAsync()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)null!));
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public async Task ThrowsAsync_WithExpectedMessage_NullAction_ThrowsArgumentNullAsync()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)null!, "expected", null));
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public async Task ThrowsAsync_ActionReturnsNullTask_ThrowsArgumentExceptionAsync()
    {
        // Awaiting a null Task would surface as an opaque NullReferenceException reported
        // against the tested code, so the delegate misuse is named instead.
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Assert.ThrowsAsync<InvalidOperationException>(() => null!));
        Assert.Contains("null Task", ex.Message);
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public async Task ThrowsAsync_WithExpectedMessage_ActionReturnsNullTask_ThrowsArgumentExceptionAsync()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Assert.ThrowsAsync<InvalidOperationException>(() => null!, "expected", null));
        Assert.Contains("null Task", ex.Message);
        Assert.Equal("action", ex.ParamName);
    }

    [Test]
    public async Task ThrowsAsync_NullReturningActionExpectedExceptionType_StillReportsMisuseAsync()
    {
        // The misuse guard must win even when the expected type would swallow it: expecting
        // ArgumentException must not let a null Task masquerade as the expected exception.
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Assert.ThrowsAsync<ArgumentException>(() => null!));
        Assert.Contains("null Task", ex.Message);
    }
}
