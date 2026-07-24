using System.Runtime.InteropServices;

namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for the runtime skip assertions. Platform-specific skips are
/// verified against the actual current OS so the suite behaves correctly on any host.
/// </summary>
public class AssertSkipTests
{
    [Test]
    public void Skip_AlwaysThrowsTestSkipped()
    {
        var ex = Assert.Throws<TestSkippedException>(() => Assert.Skip("skip reason"));
        Assert.Equal("skip reason", ex.Message);
    }

    [Test]
    public void SkipWhen_ConditionTrue_Throws()
    {
        var ex = Assert.Throws<TestSkippedException>(() => Assert.SkipWhen(true, "condition met"));
        Assert.Equal("condition met", ex.Message);
    }

    [Test]
    public void SkipWhen_ConditionFalse_DoesNotThrow()
    {
        Assert.SkipWhen(false, "not skipped");
    }

    [Test]
    public void SkipUnless_ConditionTrue_DoesNotThrow()
    {
        Assert.SkipUnless(true, "not skipped");
    }

    [Test]
    public void SkipUnless_ConditionFalse_Throws()
    {
        var ex = Assert.Throws<TestSkippedException>(() => Assert.SkipUnless(false, "condition unmet"));
        Assert.Equal("condition unmet", ex.Message);
    }

    [Test]
    public void SkipOnWindows_MatchesCurrentPlatform()
    {
        AssertPlatformSkip(OSPlatform.Windows, reason => Assert.SkipOnWindows(reason));
    }

    [Test]
    public void SkipOnLinux_MatchesCurrentPlatform()
    {
        AssertPlatformSkip(OSPlatform.Linux, reason => Assert.SkipOnLinux(reason));
    }

    [Test]
    public void SkipOnMacOS_MatchesCurrentPlatform()
    {
        AssertPlatformSkip(OSPlatform.OSX, reason => Assert.SkipOnMacOS(reason));
    }

    [Test]
    public void SkipOnFreeBSD_MatchesCurrentPlatform()
    {
        AssertPlatformSkip(OSPlatform.FreeBSD, reason => Assert.SkipOnFreeBSD(reason));
    }

    private static void AssertPlatformSkip(OSPlatform platform, Action<string> skipAction)
    {
        const string reason = "platform skip reason";
        if (RuntimeInformation.IsOSPlatform(platform))
        {
            var ex = Assert.Throws<TestSkippedException>(() => skipAction(reason));
            Assert.Equal(reason, ex.Message);
        }
        else
        {
            skipAction(reason);
        }
    }
}
