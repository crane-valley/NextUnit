namespace NextUnit;

/// <summary>
/// Provides test data from a static method or property for a parameterized test method.
/// </summary>
/// <remarks>
/// This attribute specifies a method or property that returns test data for parameterized tests.
/// The data source must be a static member that returns <see cref="System.Collections.Generic.IEnumerable{T}"/>
/// where T is <see cref="object"/>[] or a compatible type.
/// <para>
/// Rows may also be produced asynchronously, by returning
/// <see cref="System.Collections.Generic.IAsyncEnumerable{T}"/>, <c>Task&lt;TCollection&gt;</c>, or
/// <c>ValueTask&lt;TCollection&gt;</c> where <c>TCollection</c> is enumerable. An
/// <see cref="System.Collections.Generic.IAsyncEnumerable{T}"/> member may take a single
/// <see cref="System.Threading.CancellationToken"/> parameter, which receives the discovery
/// cancellation token.
/// </para>
/// <para>
/// A data source must not block synchronously. Cancellation is honored at every genuine await
/// point, but code that blocks the calling thread -- <c>Task.Wait</c>, <c>.Result</c>, a lazy
/// sequence whose <c>MoveNext</c> blocks -- cannot be interrupted by any token, and stalls
/// discovery until it returns. Wait asynchronously and observe the token instead.
/// </para>
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
