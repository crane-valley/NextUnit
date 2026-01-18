namespace NextUnit.Core;

/// <summary>
/// Provides static access to the current test context via async-local storage.
/// The context is automatically set by the test execution engine during test execution.
/// </summary>
public static class TestContext
{
    private static readonly AsyncLocal<ITestContext?> _current = new();

    /// <summary>
    /// Gets the current test context for the executing test, or <c>null</c> if called outside a test execution context.
    /// </summary>
    /// <remarks>
    /// This property uses AsyncLocal storage, ensuring proper isolation in parallel test execution.
    /// The value is automatically set by the test execution engine before test execution and cleared afterward.
    /// </remarks>
    public static ITestContext? Current => _current.Value;

    /// <summary>
    /// Sets the current test context. This method is intended for internal use by the test execution engine.
    /// </summary>
    /// <param name="context">The test context to set, or <c>null</c> to clear the context.</param>
    internal static void SetCurrent(ITestContext? context) => _current.Value = context;
}
