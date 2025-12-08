namespace NextUnit;

/// <summary>
/// Marks a test method to be skipped during execution.
/// </summary>
/// <remarks>
/// Use this attribute to temporarily disable a test without removing it from the codebase.
/// The test will appear in test results as "Skipped" with the specified reason.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Skip("Feature not yet implemented")]
/// public void FutureFeature()
/// {
///     // This test will be skipped
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SkipAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SkipAttribute"/> class.
    /// </summary>
    /// <param name="reason">The reason why the test is being skipped.</param>
    public SkipAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>
    /// Gets the reason why the test is being skipped.
    /// </summary>
    public string Reason { get; }
}
