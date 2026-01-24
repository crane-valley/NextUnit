namespace NextUnit;

/// <summary>
/// Specifies that parameter values should be retrieved from a class data source,
/// allowing Cartesian product combination with other parameter data sources.
/// </summary>
/// <remarks>
/// <para>
/// The data source class must implement <see cref="System.Collections.IEnumerable"/>
/// and have a public parameterless constructor.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// public class BrowserTypes : IEnumerable&lt;string&gt;
/// {
///     public IEnumerator&lt;string&gt; GetEnumerator()
///     {
///         yield return "Chrome";
///         yield return "Firefox";
///         yield return "Edge";
///     }
///     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
/// }
///
/// [Test]
/// public void TestMethod(
///     [Values(1, 2, 3)] int number,
///     [ValuesFrom&lt;BrowserTypes&gt;] string browser)
/// {
///     // Generates 9 test cases from Cartesian product
/// }
/// </code>
/// </para>
/// <para>
/// Use <see cref="Shared"/> to control instance lifetime:
/// <code>
/// [Test]
/// public void TestMethod(
///     [ValuesFrom&lt;ExpensiveDataSource&gt;(Shared = SharedType.PerClass)] string data)
/// {
///     // Data source instance is shared across all tests in this class
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The type of the data source class. Must implement <see cref="System.Collections.IEnumerable"/>
/// and have a public parameterless constructor.
/// </typeparam>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ValuesFromAttribute<T> : Attribute
    where T : System.Collections.IEnumerable, new()
{
    /// <summary>
    /// Gets or sets the sharing scope for the data source instance.
    /// </summary>
    /// <remarks>
    /// <para>Default is <see cref="SharedType.None"/> (new instance per parameter).</para>
    /// <para>Use higher scopes to share expensive data sources:</para>
    /// <list type="bullet">
    ///   <item><see cref="SharedType.None"/>: New instance per parameter</item>
    ///   <item><see cref="SharedType.Keyed"/>: Shared by <see cref="Key"/></item>
    ///   <item><see cref="SharedType.PerClass"/>: Shared within test class</item>
    ///   <item><see cref="SharedType.PerAssembly"/>: Shared within assembly</item>
    ///   <item><see cref="SharedType.PerSession"/>: Shared across entire test session</item>
    /// </list>
    /// </remarks>
    public SharedType Shared { get; set; } = SharedType.None;

    /// <summary>
    /// Gets or sets the key for keyed sharing.
    /// Only used when <see cref="Shared"/> is <see cref="SharedType.Keyed"/>.
    /// </summary>
    public string? Key { get; set; }
}
