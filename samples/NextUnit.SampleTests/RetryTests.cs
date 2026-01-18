namespace NextUnit.SampleTests;

public class RetryTests
{
    private static int _retryCounter;
    private static int _flakyCounter;

    [Test]
    public void RegularTestWithoutRetry()
    {
        // This test runs once and passes
        Assert.True(true);
    }

    [Test]
    [Retry(3)]
    public void RetryEventuallyPasses()
    {
        // This test fails twice and then passes on the third attempt
        var attempt = Interlocked.Increment(ref _retryCounter);
        if (attempt < 3)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on third attempt");
    }

    [Test]
    [Retry(2, 100)]
    public void RetryWithDelay()
    {
        // This test uses a 100ms delay between retries
        Assert.True(true, "This test passes immediately");
    }

    [Test]
    [Flaky("Known intermittent issue due to timing")]
    public void FlakyTestMarkedAsFlaky()
    {
        // This test is marked as flaky for documentation purposes
        Assert.True(true);
    }

    [Test]
    [Flaky]
    [Retry(3)]
    public void FlakyTestWithRetry()
    {
        // This test is both flaky and has retry enabled
        var attempt = Interlocked.Increment(ref _flakyCounter);
        if (attempt < 2)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on second attempt");
    }

    [Before(LifecycleScope.Class)]
    public void ResetCounters()
    {
        // Reset counters before each class run using thread-safe operations
        Interlocked.Exchange(ref _retryCounter, 0);
        Interlocked.Exchange(ref _flakyCounter, 0);
    }
}

[Retry(2)]
public class ClassLevelRetryTests
{
    private static int _classRetryCounter;

    [Test]
    public void InheritedRetryFromClass()
    {
        // This test inherits retry count from the class
        var attempt = Interlocked.Increment(ref _classRetryCounter);
        if (attempt < 2)
        {
            Assert.True(false, $"Intentional failure on attempt {attempt}");
        }
        Assert.True(true, "Passed on second attempt");
    }

    [Test]
    [Retry(3)]
    public void MethodOverridesClassRetry()
    {
        // Method-level retry overrides class-level
        Assert.True(true, "This test has 3 retries");
    }

    [Before(LifecycleScope.Class)]
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _classRetryCounter, 0);
    }
}

[Flaky("All tests in this class are known to be flaky")]
public class FlakyTestClass
{
    [Test]
    public void AllTestsInClassAreFlaky()
    {
        Assert.True(true);
    }

    [Test]
    [Flaky("This specific test has its own flaky reason")]
    public void IndividualFlakyReason()
    {
        Assert.True(true);
    }
}
