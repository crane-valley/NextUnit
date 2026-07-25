namespace NextUnit;

public static partial class Assert
{
    /// <summary>
    /// Skips the current test with a specified reason.
    /// </summary>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Always thrown to indicate the test should be skipped.</exception>
    /// <remarks>
    /// Use this method to skip a test at runtime based on dynamic conditions.
    /// For compile-time skipping, use the <see cref="SkipAttribute"/> instead.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Skip(string reason)
    {
        throw new TestSkippedException(reason);
    }

    /// <summary>
    /// Skips the current test if the specified condition is true.
    /// </summary>
    /// <param name="condition">If <c>true</c>, the test will be skipped.</param>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Thrown when the condition is true.</exception>
    public static void SkipWhen(bool condition, string reason)
    {
        if (condition)
        {
            throw new TestSkippedException(reason);
        }
    }

    /// <summary>
    /// Skips the current test unless the specified condition is true.
    /// </summary>
    /// <param name="condition">If <c>false</c>, the test will be skipped.</param>
    /// <param name="reason">The reason for skipping the test.</param>
    /// <exception cref="TestSkippedException">Thrown when the condition is false.</exception>
    public static void SkipUnless(bool condition, string reason)
    {
        if (!condition)
        {
            throw new TestSkippedException(reason);
        }
    }

    /// <summary>
    /// Skips the current test when running on the specified operating system.
    /// </summary>
    /// <param name="platform">The platform to skip on.</param>
    /// <param name="reason">The caller-supplied reason, or <c>null</c> to use <paramref name="defaultReason"/>.</param>
    /// <param name="defaultReason">The message used when the caller supplies no reason.</param>
    /// <exception cref="TestSkippedException">Thrown when running on the specified platform.</exception>
    private static void SkipOnOS(
        System.Runtime.InteropServices.OSPlatform platform,
        string? reason,
        string defaultReason)
    {
        SkipWhen(
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(platform),
            reason ?? defaultReason);
    }

    /// <summary>
    /// Skips the current test when running on Windows.
    /// </summary>
    /// <param name="reason">The reason for skipping on Windows. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on Windows.</exception>
    public static void SkipOnWindows(string? reason = null)
    {
        SkipOnOS(
            System.Runtime.InteropServices.OSPlatform.Windows,
            reason,
            "Test skipped on Windows.");
    }

    /// <summary>
    /// Skips the current test when running on Linux.
    /// </summary>
    /// <param name="reason">The reason for skipping on Linux. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on Linux.</exception>
    public static void SkipOnLinux(string? reason = null)
    {
        SkipOnOS(
            System.Runtime.InteropServices.OSPlatform.Linux,
            reason,
            "Test skipped on Linux.");
    }

    /// <summary>
    /// Skips the current test when running on macOS.
    /// </summary>
    /// <param name="reason">The reason for skipping on macOS. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on macOS.</exception>
    public static void SkipOnMacOS(string? reason = null)
    {
        SkipOnOS(
            System.Runtime.InteropServices.OSPlatform.OSX,
            reason,
            "Test skipped on macOS.");
    }

    /// <summary>
    /// Skips the current test when running on FreeBSD.
    /// </summary>
    /// <param name="reason">The reason for skipping on FreeBSD. If null, a default message is used.</param>
    /// <exception cref="TestSkippedException">Thrown when running on FreeBSD.</exception>
    public static void SkipOnFreeBSD(string? reason = null)
    {
        SkipOnOS(
            System.Runtime.InteropServices.OSPlatform.FreeBSD,
            reason,
            "Test skipped on FreeBSD.");
    }
}
