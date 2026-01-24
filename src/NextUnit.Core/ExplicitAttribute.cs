namespace NextUnit;

/// <summary>
/// Marks a test or test class as explicit, meaning it will not run during normal test execution.
/// Explicit tests only run when specifically selected or when the --explicit flag is used.
/// </summary>
/// <remarks>
/// Use this attribute for tests that:
/// <list type="bullet">
/// <item><description>Require manual setup or specific environment conditions</description></item>
/// <item><description>Take a very long time to execute</description></item>
/// <item><description>Are used for debugging or exploration purposes</description></item>
/// <item><description>Interact with external services that aren't always available</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Explicit("Requires database connection")]
/// public void TestWithDatabaseDependency()
/// {
///     // This test won't run by default
/// }
///
/// [Explicit("Long-running performance tests")]
/// public class PerformanceTests
/// {
///     [Test]
///     public void BenchmarkTest() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ExplicitAttribute : Attribute
{
    /// <summary>
    /// Marks a test or test class as explicit without a reason.
    /// </summary>
    public ExplicitAttribute()
    {
    }

    /// <summary>
    /// Marks a test or test class as explicit with the specified reason.
    /// </summary>
    /// <param name="reason">The reason why this test is marked as explicit.</param>
    public ExplicitAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the reason why the test is marked as explicit, if provided.
    /// </summary>
    public string? Reason { get; }
}
