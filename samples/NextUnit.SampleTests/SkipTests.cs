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

    // Runtime Skip Tests

    [Test]
    public void RuntimeSkipWithReason()
    {
        // Simulating a runtime condition check
        var requiredService = Environment.GetEnvironmentVariable("REQUIRED_SERVICE_FOR_TEST");
        if (string.IsNullOrEmpty(requiredService))
        {
            Assert.Skip("Required service environment variable not set");
        }

        Assert.True(true);
    }

    [Test]
    public void ConditionalSkipWhen()
    {
        // Skip this test on non-64-bit processes
        Assert.SkipWhen(!Environment.Is64BitProcess, "Test requires 64-bit process");
        Assert.True(Environment.Is64BitProcess);
    }

    [Test]
    public void ConditionalSkipUnless()
    {
        // Skip unless running as 64-bit
        Assert.SkipUnless(Environment.Is64BitProcess, "Test requires 64-bit process");
        Assert.True(Environment.Is64BitProcess);
    }

    [Test]
    public void PlatformSpecificSkipOnLinux()
    {
        // This test only runs on Windows and macOS
        Assert.SkipOnLinux("This test does not run on Linux");
        Assert.True(true, "Test ran successfully on non-Linux platform");
    }

    [Test]
    public void PlatformSpecificSkipOnMacOS()
    {
        // This test only runs on Windows and Linux
        Assert.SkipOnMacOS("This test does not run on macOS");
        Assert.True(true, "Test ran successfully on non-macOS platform");
    }

    [Test]
    public void RuntimeSkipBasedOnCondition()
    {
        // Example of conditional skip based on runtime environment
        var debugMode = false;
#if DEBUG
        debugMode = true;
#endif
        Assert.SkipWhen(!debugMode, "This test only runs in DEBUG mode");
        Assert.True(debugMode);
    }
}
