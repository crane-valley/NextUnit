namespace NextUnit;

/// <summary>
/// Specifies a custom display name for a test method.
/// </summary>
/// <remarks>
/// The display name appears in Test Explorer and test reports instead of the method name.
/// For parameterized tests, use placeholders <c>{0}</c>, <c>{1}</c>, etc. to include argument values.
/// </remarks>
/// <example>
/// <code>
/// // Simple custom display name
/// [Test]
/// [DisplayName("User login should succeed with valid credentials")]
/// public void UserLogin_ValidCredentials_Succeeds()
/// {
///     // Test implementation
/// }
///
/// // With placeholders for parameterized tests
/// [Test]
/// [DisplayName("Adding {0} + {1} should equal {2}")]
/// [Arguments(1, 2, 3)]
/// [Arguments(5, 5, 10)]
/// public void Add_ReturnsCorrectSum(int a, int b, int expected)
/// {
///     Assert.Equal(expected, a + b);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DisplayNameAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The custom display name for the test. Supports {0}, {1}, etc. placeholders for parameterized tests.</param>
    public DisplayNameAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the custom display name.
    /// </summary>
    public string Name { get; }
}
