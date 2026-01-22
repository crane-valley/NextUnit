namespace NextUnit.SampleTests;

/// <summary>
/// Basic test examples demonstrating NextUnit core functionality.
/// </summary>
public class BasicTests
{
    [Test]
    public void SimplePassing()
    {
        Assert.True(true);
    }

    [Test]
    public void SimpleEquality()
    {
        var expected = 42;
        var actual = 40 + 2;
        Assert.Equal(expected, actual);
    }

    [Test]
    public void NullChecks()
    {
        string? nullValue = null;
        string nonNullValue = "test";

        Assert.Null(nullValue);
        Assert.NotNull(nonNullValue);
    }

    [Test]
    public void ExceptionHandling()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            throw new InvalidOperationException("Expected error");
        });

        Assert.NotNull(ex);
        Assert.Equal("Expected error", ex.Message);
    }

    [Test]
    public async Task AsyncTestAsync()
    {
        await Task.Delay(10);
        Assert.True(true);
    }

    [Test]
    public async Task AsyncExceptionHandlingAsync()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Async error");
        });

        Assert.Equal("Async error", ex.Message);
    }
}
