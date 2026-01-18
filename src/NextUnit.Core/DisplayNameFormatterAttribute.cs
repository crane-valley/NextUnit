using System.Diagnostics.CodeAnalysis;

namespace NextUnit;

/// <summary>
/// Specifies a custom display name formatter type for a test method or class.
/// </summary>
/// <remarks>
/// The formatter type must implement <see cref="IDisplayNameFormatter"/> and have a parameterless constructor.
/// When applied to a class, all test methods in the class will use the formatter unless overridden
/// by a method-level <see cref="DisplayNameAttribute"/> or <see cref="DisplayNameFormatterAttribute"/>.
/// </remarks>
/// <example>
/// <code>
/// public class HumanReadableFormatter : IDisplayNameFormatter
/// {
///     public string Format(DisplayNameContext context)
///     {
///         return Regex.Replace(context.MethodName, "([a-z])([A-Z])", "$1 $2")
///                     .Replace("_", " ")
///                     .ToLowerInvariant();
///     }
/// }
///
/// [DisplayNameFormatter(typeof(HumanReadableFormatter))]
/// public class UserTests
/// {
///     [Test]
///     public void UserLogin_ValidCredentials_Succeeds() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DisplayNameFormatterAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameFormatterAttribute"/> class.
    /// </summary>
    /// <param name="formatterType">The type that implements <see cref="IDisplayNameFormatter"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="formatterType"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="formatterType"/> does not implement <see cref="IDisplayNameFormatter"/>.
    /// </exception>
    public DisplayNameFormatterAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type formatterType)
    {
        ArgumentNullException.ThrowIfNull(formatterType);

        if (!typeof(IDisplayNameFormatter).IsAssignableFrom(formatterType))
        {
            throw new ArgumentException(
                $"Type '{formatterType.FullName}' must implement IDisplayNameFormatter.",
                nameof(formatterType));
        }

        FormatterType = formatterType;
    }

    /// <summary>
    /// Gets the formatter type.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type FormatterType { get; }
}

/// <summary>
/// Specifies a custom display name formatter type for a test method or class using generic syntax.
/// </summary>
/// <typeparam name="TFormatter">The formatter type that implements <see cref="IDisplayNameFormatter"/>.</typeparam>
/// <remarks>
/// This is a generic version of <see cref="DisplayNameFormatterAttribute"/> that provides cleaner syntax
/// and compile-time type checking.
/// When applied to a class, all test methods in the class will use the formatter unless overridden
/// by a method-level <see cref="DisplayNameAttribute"/> or <see cref="DisplayNameFormatterAttribute{TFormatter}"/>.
/// </remarks>
/// <example>
/// <code>
/// public class HumanReadableFormatter : IDisplayNameFormatter
/// {
///     public string Format(DisplayNameContext context)
///     {
///         return Regex.Replace(context.MethodName, "([a-z])([A-Z])", "$1 $2")
///                     .Replace("_", " ")
///                     .ToLowerInvariant();
///     }
/// }
///
/// [DisplayNameFormatter&lt;HumanReadableFormatter&gt;]
/// public class UserTests
/// {
///     [Test]
///     public void UserLogin_ValidCredentials_Succeeds() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class DisplayNameFormatterAttribute<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TFormatter> : Attribute
    where TFormatter : IDisplayNameFormatter, new()
{
    /// <summary>
    /// Gets the formatter type.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type FormatterType => typeof(TFormatter);
}
