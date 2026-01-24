namespace NextUnit;

/// <summary>
/// Specifies that parameter values should be retrieved from a static member
/// (property, field, or method), allowing Cartesian product combination
/// with other parameter data sources.
/// </summary>
/// <remarks>
/// <para>
/// The member must be static and return an <see cref="System.Collections.IEnumerable"/>.
/// If <see cref="MemberType"/> is not specified, the member is looked up
/// in the test class.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// public class MyTests
/// {
///     public static IEnumerable&lt;int&gt; Numbers => [1, 2, 3];
///     public static IEnumerable&lt;string&gt; GetLetters() => ["a", "b"];
///
///     [Test]
///     public void TestMethod(
///         [ValuesFromMember(nameof(Numbers))] int number,
///         [ValuesFromMember(nameof(GetLetters))] string letter)
///     {
///         // Generates 6 test cases from Cartesian product
///     }
/// }
/// </code>
/// </para>
/// <para>
/// To use a member from another type:
/// <code>
/// [Test]
/// public void TestMethod(
///     [ValuesFromMember("Values", MemberType = typeof(DataProvider))] int value)
/// {
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class ValuesFromMemberAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValuesFromMemberAttribute"/> class
    /// with the specified member name.
    /// </summary>
    /// <param name="memberName">
    /// The name of the static member (property, field, or method) that provides values.
    /// </param>
    public ValuesFromMemberAttribute(string memberName)
    {
        MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
    }

    /// <summary>
    /// Gets the name of the member that provides values.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Gets or sets the type that contains the member.
    /// If not specified, the test class is used.
    /// </summary>
    public Type? MemberType { get; set; }
}
