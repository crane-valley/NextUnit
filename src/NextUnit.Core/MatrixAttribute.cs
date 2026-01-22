namespace NextUnit;

/// <summary>
/// Specifies the values for a parameter in a matrix test.
/// When multiple parameters have <see cref="MatrixAttribute"/>, the test framework
/// generates the Cartesian product of all value combinations.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="ArgumentsAttribute"/> which defines complete argument sets,
/// <see cref="MatrixAttribute"/> defines values per parameter. The framework
/// automatically generates all combinations.
/// </para>
/// <para>
/// All parameters in a matrix test method must have the <see cref="MatrixAttribute"/>.
/// Use <see cref="MatrixExclusionAttribute"/> to skip specific combinations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// public void TestAdd(
///     [Matrix(1, 2, 3)] int a,
///     [Matrix(10, 20)] int b)
/// {
///     // Generates 6 test cases: (1,10), (1,20), (2,10), (2,20), (3,10), (3,20)
///     Assert.True(a + b > 0);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class MatrixAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatrixAttribute"/> class.
    /// </summary>
    /// <param name="values">The values to use for this parameter in the matrix.</param>
    public MatrixAttribute(params object?[] values)
    {
        Values = values ?? Array.Empty<object?>();
    }

    /// <summary>
    /// Gets the values for this parameter.
    /// </summary>
    public object?[] Values { get; }
}
