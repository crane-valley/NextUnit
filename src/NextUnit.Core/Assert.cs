namespace NextUnit;

/// <summary>
/// Provides assertion methods for verifying test conditions.
/// </summary>
public static partial class Assert
{
    /// <summary>
    /// Verifies that a condition is true.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the condition is false.</exception>
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new AssertionFailedException(message ?? "Expected true but was false.");
        }
    }

    /// <summary>
    /// Verifies that a condition is false.
    /// </summary>
    /// <param name="condition">The condition to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the condition is true.</exception>
    public static void False(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new AssertionFailedException(message ?? "Expected false but was true.");
        }
    }

    /// <summary>
    /// Verifies that a value is null.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is not null.</exception>
    public static void Null(object? value, string? message = null)
    {
        if (value is not null)
        {
            throw new AssertionFailedException(message ?? "Expected null.");
        }
    }

    /// <summary>
    /// Verifies that a value is not null.
    /// </summary>
    /// <param name="value">The value to verify.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="AssertionFailedException">Thrown when the value is null.</exception>
    public static void NotNull(object? value, string? message = null)
    {
        if (value is null)
        {
            throw new AssertionFailedException(message ?? "Expected non-null.");
        }
    }

    /// <summary>
    /// Verifies that two objects refer to the same instance using reference equality.
    /// </summary>
    /// <param name="expected">The expected instance.</param>
    /// <param name="actual">The actual instance.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="ArgumentException">Thrown when either argument is a value type, whose boxing makes reference identity meaningless.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the objects are not the same instance.</exception>
    /// <remarks>
    /// This is a reference-identity check. Value-type arguments are boxed into fresh objects,
    /// so <see cref="object.ReferenceEquals"/> would almost never hold; passing one is rejected
    /// as a test-authoring mistake. Use <see cref="Equal{T}(T, T, string?)"/> for value equality.
    /// </remarks>
    public static void Same(object? expected, object? actual, string? message = null)
    {
        ThrowIfValueType(expected, nameof(expected));
        ThrowIfValueType(actual, nameof(actual));

        if (!ReferenceEquals(expected, actual))
        {
            throw new AssertionFailedException(
                message ?? $"Expected both arguments to reference the same instance.\nExpected: {expected}\nActual: {actual}");
        }
    }

    /// <summary>
    /// Verifies that two objects refer to different instances using reference equality.
    /// </summary>
    /// <param name="expected">The instance that should not be the same as <paramref name="actual"/>.</param>
    /// <param name="actual">The actual instance.</param>
    /// <param name="message">Optional custom message to display if the assertion fails.</param>
    /// <exception cref="ArgumentException">Thrown when either argument is a value type, whose boxing makes reference identity meaningless.</exception>
    /// <exception cref="AssertionFailedException">Thrown when the objects are the same instance.</exception>
    /// <remarks>
    /// This is a reference-identity check. Value-type arguments are boxed into fresh objects,
    /// so <see cref="object.ReferenceEquals"/> would almost never hold; passing one is rejected
    /// as a test-authoring mistake. Use <see cref="NotEqual{T}(T, T, string?)"/> for value inequality.
    /// </remarks>
    public static void NotSame(object? expected, object? actual, string? message = null)
    {
        ThrowIfValueType(expected, nameof(expected));
        ThrowIfValueType(actual, nameof(actual));

        if (ReferenceEquals(expected, actual))
        {
            throw new AssertionFailedException(
                message ?? "Expected the arguments to reference different instances, but they were the same instance.");
        }
    }

    // Guards Same/NotSame against boxed value types: reference identity on a fresh box is
    // meaningless, so a value-type argument is a test-authoring bug rather than a failed
    // assertion. null is a reference (not a ValueType), so null arguments are allowed.
    private static void ThrowIfValueType(object? argument, string parameterName)
    {
        if (argument is ValueType)
        {
            throw new ArgumentException(
                $"Reference-identity assertions (Same/NotSame) do not support value types; '{argument.GetType().Name}' is boxed into a fresh object, making reference identity meaningless. Use Equal/NotEqual for value comparison.",
                parameterName);
        }
    }

    /// <summary>
    /// Fails the current test unconditionally.
    /// </summary>
    /// <param name="message">Optional custom message describing the failure.</param>
    /// <exception cref="AssertionFailedException">Always thrown.</exception>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Fail(string? message = null)
    {
        throw new AssertionFailedException(message ?? "Assert.Fail() was called.");
    }
}
