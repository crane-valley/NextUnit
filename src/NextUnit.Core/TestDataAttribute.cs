namespace NextUnit;

/// <summary>
/// Provides test data from a static method or property for a parameterized test method.
/// </summary>
/// <remarks>
/// This attribute specifies a method or property that returns test data for parameterized tests.
/// The data source must be a static member that returns <see cref="System.Collections.Generic.IEnumerable{T}"/> 
/// where T is <see cref="object"/>[] or a compatible type.
/// </remarks>
/// <example>
/// <code>
/// public class MathTests
/// {
///     public static IEnumerable&lt;object[]&gt; AddTestData()
///     {
///         yield return new object[] { 1, 2, 3 };
///         yield return new object[] { 2, 3, 5 };
///         yield return new object[] { -1, 1, 0 };
///     }
/// 
///     [Test]
///     [TestData(nameof(AddTestData))]
///     public void Add_ReturnsCorrectSum(int a, int b, int expected)
///     {
///         var result = a + b;
///         Assert.Equal(expected, result);
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class TestDataAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataAttribute"/> class.
    /// </summary>
    /// <param name="memberName">The name of the static method or property that provides test data.</param>
    public TestDataAttribute(string memberName)
    {
        MemberName = memberName;
    }

    /// <summary>
    /// Gets the name of the member that provides test data.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets or sets the type that contains the data member.
    /// If null, the test class itself is used.
    /// </summary>
    public Type? MemberType { get; set; }
}
