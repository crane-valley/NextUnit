namespace NextUnit;

/// <summary>
/// Specifies how a ClassDataSource instance is shared across tests.
/// </summary>
public enum SharedType
{
    /// <summary>
    /// New instance for each test method (default).
    /// </summary>
    None = 0,

    /// <summary>
    /// Shared by key value across all tests with the same key.
    /// </summary>
    Keyed = 1,

    /// <summary>
    /// Single instance shared within the same test class.
    /// </summary>
    PerClass = 2,

    /// <summary>
    /// Single instance shared across all tests in the assembly.
    /// </summary>
    PerAssembly = 3,

    /// <summary>
    /// Single instance shared across the entire test session.
    /// </summary>
    PerSession = 4
}

/// <summary>
/// Provides test data from a class that implements <see cref="System.Collections.IEnumerable"/>.
/// </summary>
/// <typeparam name="T">The type that provides test data. Must implement <see cref="System.Collections.IEnumerable"/> and have a parameterless constructor.</typeparam>
/// <remarks>
/// <para>
/// The data source class must implement <see cref="System.Collections.IEnumerable"/> (typically <see cref="IEnumerable{T}"/> where T is <see cref="object"/>[])
/// and have a public parameterless constructor.
/// </para>
/// <para>
/// Use the <see cref="Shared"/> property to control instance lifetime:
/// <list type="bullet">
///   <item><see cref="SharedType.None"/>: New instance for each test method (default)</item>
///   <item><see cref="SharedType.Keyed"/>: Shared by <see cref="Key"/> value</item>
///   <item><see cref="SharedType.PerClass"/>: Shared within the test class</item>
///   <item><see cref="SharedType.PerAssembly"/>: Shared across the assembly</item>
///   <item><see cref="SharedType.PerSession"/>: Shared across the entire session</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MultiplicationTestData : IEnumerable&lt;object?[]&gt;
/// {
///     public IEnumerator&lt;object?[]&gt; GetEnumerator()
///     {
///         yield return [2, 3, 6];
///         yield return [4, 5, 20];
///     }
///     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
/// }
///
/// public class MathTests
/// {
///     [Test]
///     [ClassDataSource&lt;MultiplicationTestData&gt;]
///     public void Multiply_ReturnsCorrectProduct(int a, int b, int expected)
///     {
///         Assert.Equal(expected, a * b);
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ClassDataSourceAttribute<T> : Attribute
    where T : System.Collections.IEnumerable, new()
{
    /// <summary>
    /// Gets or sets the sharing scope for the data source instance.
    /// </summary>
    public SharedType Shared { get; set; } = SharedType.None;

    /// <summary>
    /// Gets or sets the key for keyed sharing.
    /// Required when <see cref="Shared"/> is <see cref="SharedType.Keyed"/>.
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>
/// Provides test data from multiple classes that implement <see cref="System.Collections.IEnumerable"/>.
/// Data from all source types is combined.
/// </summary>
/// <typeparam name="T1">The first data source type.</typeparam>
/// <typeparam name="T2">The second data source type.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ClassDataSourceAttribute<T1, T2> : Attribute
    where T1 : System.Collections.IEnumerable, new()
    where T2 : System.Collections.IEnumerable, new()
{
    /// <summary>
    /// Gets or sets the sharing scope for the data source instances.
    /// </summary>
    public SharedType Shared { get; set; } = SharedType.None;

    /// <summary>
    /// Gets or sets the key for keyed sharing.
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>
/// Provides test data from multiple classes that implement <see cref="System.Collections.IEnumerable"/>.
/// Data from all source types is combined.
/// </summary>
/// <typeparam name="T1">The first data source type.</typeparam>
/// <typeparam name="T2">The second data source type.</typeparam>
/// <typeparam name="T3">The third data source type.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ClassDataSourceAttribute<T1, T2, T3> : Attribute
    where T1 : System.Collections.IEnumerable, new()
    where T2 : System.Collections.IEnumerable, new()
    where T3 : System.Collections.IEnumerable, new()
{
    /// <summary>
    /// Gets or sets the sharing scope for the data source instances.
    /// </summary>
    public SharedType Shared { get; set; } = SharedType.None;

    /// <summary>
    /// Gets or sets the key for keyed sharing.
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>
/// Provides test data from multiple classes that implement <see cref="System.Collections.IEnumerable"/>.
/// Data from all source types is combined.
/// </summary>
/// <typeparam name="T1">The first data source type.</typeparam>
/// <typeparam name="T2">The second data source type.</typeparam>
/// <typeparam name="T3">The third data source type.</typeparam>
/// <typeparam name="T4">The fourth data source type.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ClassDataSourceAttribute<T1, T2, T3, T4> : Attribute
    where T1 : System.Collections.IEnumerable, new()
    where T2 : System.Collections.IEnumerable, new()
    where T3 : System.Collections.IEnumerable, new()
    where T4 : System.Collections.IEnumerable, new()
{
    /// <summary>
    /// Gets or sets the sharing scope for the data source instances.
    /// </summary>
    public SharedType Shared { get; set; } = SharedType.None;

    /// <summary>
    /// Gets or sets the key for keyed sharing.
    /// </summary>
    public string? Key { get; set; }
}
