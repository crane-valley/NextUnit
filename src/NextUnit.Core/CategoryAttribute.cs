namespace NextUnit;

/// <summary>
/// Assigns a category to a test method or test class for filtering and organization.
/// </summary>
/// <remarks>
/// Categories can be used to group related tests and enable selective test execution.
/// Multiple categories can be applied by using the attribute multiple times.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Category("Integration")]
/// [Category("Database")]
/// public void DatabaseConnection_Succeeds()
/// {
///     // Test implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class CategoryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the category.</param>
    public CategoryAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string Name { get; }
}

/// <summary>
/// Assigns a tag to a test method or test class for filtering and organization.
/// </summary>
/// <remarks>
/// Tags provide a flexible way to mark tests with arbitrary metadata.
/// Multiple tags can be applied by using the attribute multiple times.
/// Tags are similar to categories but intended for more fine-grained classification.
/// </remarks>
/// <example>
/// <code>
/// [Test]
/// [Tag("Slow")]
/// [Tag("RequiresNetwork")]
/// public void ExternalApi_ReturnsData()
/// {
///     // Test implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class TagAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the tag.</param>
    public TagAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the tag name.
    /// </summary>
    public string Name { get; }
}
