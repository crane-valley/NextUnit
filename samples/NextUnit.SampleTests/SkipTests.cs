using NextUnit;

namespace NextUnit.SampleTests;

public class SkipTests
{
    [Test]
    public void ThisTestRuns()
    {
        Assert.True(true);
    }

    [Test]
    [Skip("This feature is not yet implemented")]
    public void SkippedWithReason()
    {
        // This test should be skipped
        Assert.True(false, "This should not be executed");
    }

    [Test]
    [Skip("Waiting for bug fix #123")]
    public void SkippedWaitingForFix()
    {
        // This test should be skipped
        throw new InvalidOperationException("This should not be executed");
    }

    [Test]
    public void AnotherTestThatRuns()
    {
        Assert.Equal(2 + 2, 4);
    }
}
