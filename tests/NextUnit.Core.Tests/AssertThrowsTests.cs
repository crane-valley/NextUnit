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
}
