namespace NextUnit;

/// <summary>
/// Specifies a combination of parameter values to exclude from a matrix test.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is used in conjunction with <see cref="MatrixAttribute"/> to exclude
/// specific combinations from the generated test cases. The values must match the order
/// of parameters in the test method.
/// </para>
/// <para>
/// Multiple <see cref="MatrixExclusionAttribute"/> instances can be applied to exclude
/// multiple combinations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [MatrixExclusion(1, 10)]  // Skip combination where a=1 and b=10
/// [MatrixExclusion(2, 20)]  // Also skip combination where a=2 and b=20
/// public void TestAdd(
///     [Matrix(1, 2, 3)] int a,
///     [Matrix(10, 20)] int b)
/// {
///     // Generates 4 test cases instead of 6 (excluding (1,10) and (2,20))
///     Assert.True(a + b > 0);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MatrixExclusionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixExclusionAttribute"/> class.
    /// </summary>
    /// <param name="values">The combination of values to exclude, in parameter order.</param>
    public MatrixExclusionAttribute(params object?[] values)
    {
        Values = values ?? Array.Empty<object?>();
    }

    /// <summary>
    /// Gets the combination of values to exclude.
    /// </summary>
    public object?[] Values { get; }
}
