namespace NextUnit;

/// <summary>
/// Defines a contract for custom test display name formatting.
/// </summary>
/// <remarks>
/// Implement this interface to provide custom logic for generating test display names.
/// The formatter is instantiated once per test class and cached for reuse during discovery.
/// </remarks>
/// <example>
/// <code>
/// public class HumanReadableFormatter : IDisplayNameFormatter
/// {
///     public string Format(DisplayNameContext context)
///     {
///         // Convert "UserLogin_ValidCredentials_Succeeds" to "User login valid credentials succeeds"
///         var result = Regex.Replace(context.MethodName, "([a-z])([A-Z])", "$1 $2")
///                           .Replace("_", " ")
///                           .ToLowerInvariant();
///
///         if (context.Arguments is { Length: > 0 })
///         {
///             var args = string.Join(", ", context.Arguments.Select(a => a?.ToString() ?? "null"));
///             result += $" ({args})";
///         }
///
///         return result;
///     }
/// }
/// </code>
/// </example>
public interface IDisplayNameFormatter
{
    /// <summary>
    /// Formats a display name for a test method.
    /// </summary>
    /// <param name="context">Context containing test method information.</param>
    /// <returns>The formatted display name.</returns>
    public string Format(DisplayNameContext context);
}

/// <summary>
/// Contains context information for display name formatting.
/// </summary>
public readonly struct DisplayNameContext
{
    /// <summary>
    /// Gets the original method name.
    /// </summary>
    public string MethodName { get; init; }

    /// <summary>
    /// Gets the test class type.
    /// </summary>
    public Type TestClass { get; init; }

    /// <summary>
    /// Gets the test arguments for parameterized tests, or null for non-parameterized tests.
    /// </summary>
    public object?[]? Arguments { get; init; }

    /// <summary>
    /// Gets the zero-based index of the current test case for parameterized tests, or -1 for non-parameterized tests.
    /// </summary>
    public int ArgumentSetIndex { get; init; }
}
