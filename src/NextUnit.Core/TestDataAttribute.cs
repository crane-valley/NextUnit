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
/// Rows are enumerated during discovery by default, which keeps every row an individually
/// selectable and filterable test case. Set <see cref="DeferredEnumeration"/> to move the
/// enumeration to execution time for a source too large to expand at startup.
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

    /// <summary>
    /// Gets or sets a value indicating whether the rows are enumerated during execution instead of
    /// during discovery. The default is <see langword="false"/>, which enumerates during discovery.
    /// </summary>
    /// <remarks>
    /// Opt in only for a source large or slow enough that expanding it at startup is the problem
    /// being solved. Discovery then reports one placeholder test for the whole source and the rows
    /// become individual test results only once the run reaches them.
    /// <para>
    /// The cost is selection and filtering granularity. Nothing but the placeholder exists at
    /// discovery, so a deferred source is selected, filtered, and skipped as one unit: row display
    /// names, row categories, and row tags cannot be filtered on, and an IDE cannot run a single
    /// row. Filters still apply to the group through the test method's own name, categories, and
    /// tags. A filter is never allowed to silently re-enable eager enumeration, because that would
    /// reintroduce the startup cost this option exists to remove.
    /// </para>
    /// <para>
    /// Deferring moves when the member is called, and that is visible in the lifecycle. An eager
    /// source is read while the test list is built, before any hook runs. A deferred source is read
    /// at the start of the run, which under Microsoft.Testing.Platform is after session-scoped
    /// setup has run and before assembly-, class-, and test-scoped hooks; the VSTest adapter has no
    /// session scope, so nothing has run there either. A deferred source is also not read at all
    /// when a session setup hook requested a skip, or when the test itself is skipped. Do not write
    /// a data source that depends on lifecycle state: the ordering differs between the two modes
    /// and is not a contract to build on.
    /// </para>
    /// <para>
    /// A source that fails is reported as an error on the placeholder and the rest of the run
    /// continues.
    /// </para>
    /// </remarks>
    public bool DeferredEnumeration { get; set; }
}
