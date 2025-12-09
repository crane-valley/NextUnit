namespace NextUnit;

/// <summary>
/// Provides inline test data for a parameterized test method.
/// </summary>
/// <remarks>
/// Use this attribute multiple times on a test method to create multiple test cases with different arguments.
/// The attribute can be applied multiple times to run the same test with different data sets.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Arguments(2, 3, 5)]
/// [Arguments(1, 1, 2)]
/// [Arguments(-1, 1, 0)]
/// public void Add_ReturnsCorrectSum(int a, int b, int expected)
/// {
///     var result = a + b;
///     Assert.Equal(expected, result);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ArgumentsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArgumentsAttribute"/> class.
    /// </summary>
    /// <param name="args">The arguments to pass to the test method.</param>
    public ArgumentsAttribute(params object?[] args)
    {
        Arguments = args;
    }

    /// <summary>
    /// Gets the arguments to pass to the test method.
    /// </summary>
    public object?[] Arguments { get; }
}
