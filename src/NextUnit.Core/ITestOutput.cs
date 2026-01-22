namespace NextUnit.Core;

/// <summary>
/// Provides test output capabilities for writing diagnostic messages during test execution.
/// This interface is injected into test class constructors, similar to xUnit's ITestOutputHelper.
/// Output is captured per-test and included in test results.
/// </summary>
public interface ITestOutput
{
    /// <summary>
    /// Writes a line of text to the test output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void WriteLine(string message);

    /// <summary>
    /// Writes formatted text to the test output.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The format arguments.</param>
    public void WriteLine(string format, params object?[] args);
}
