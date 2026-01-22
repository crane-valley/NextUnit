namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating the [Repeat] attribute functionality.
/// </summary>
public class RepeatTests
{
    [Test]
    [Repeat(3)]
    public void TestRunsThreeTimes()
    {
        var index = TestContext.Current?.RepeatIndex;
        TestContext.Current?.Output.WriteLine($"Repeat index: {index}");
        Assert.True(index >= 0 && index < 3);
    }

    [Test]
    [Repeat(2)]
    public void RepeatIndex_IsAccessibleViaTestContext()
    {
        var ctx = TestContext.Current;
        Assert.NotNull(ctx);
        Assert.NotNull(ctx!.RepeatIndex);
        Assert.True(ctx.RepeatIndex >= 0 && ctx.RepeatIndex < 2);
    }

    [Test]
    [Arguments(10, 20)]
    [Arguments(30, 40)]
    [Repeat(2)]
    public void ArgumentsWithRepeat_CombinesCorrectly(int a, int b)
    {
        // 2 argument sets x 2 repeats = 4 test cases
        var ctx = TestContext.Current;
        Assert.NotNull(ctx);
        Assert.NotNull(ctx!.RepeatIndex);
        ctx.Output.WriteLine($"Arguments: ({a}, {b}), RepeatIndex: {ctx.RepeatIndex}");
        Assert.True(a > 0 && b > 0);
    }

    [Test]
    public void NonRepeatedTest_HasNullRepeatIndex()
    {
        var ctx = TestContext.Current;
        Assert.NotNull(ctx);
        Assert.Null(ctx!.RepeatIndex);
    }
}
