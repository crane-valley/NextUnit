namespace NextUnit;

/// <summary>
/// Specifies inline values for a test parameter, allowing Cartesian product
/// combination with other parameter data sources.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute on test method parameters to provide inline values.
/// When multiple parameters have data source attributes, the framework
/// generates test cases for all combinations (Cartesian product).
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Test]
/// public void TestMethod(
///     [Values(1, 2, 3)] int number,
///     [Values("a", "b")] string letter)
/// {
///     // Generates 6 test cases: (1,a), (1,b), (2,a), (2,b), (3,a), (3,b)
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ValuesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValuesAttribute"/> class
    /// with the specified values.
    /// </summary>
    /// <param name="values">The values to use for this parameter.</param>
    public ValuesAttribute(params object?[] values)
    {
        Values = values ?? [];
    }

    /// <summary>
    /// Gets the values to use for this parameter.
    /// </summary>
    public object?[] Values { get; }
}
