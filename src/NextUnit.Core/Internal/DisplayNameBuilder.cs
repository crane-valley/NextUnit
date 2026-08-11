using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace NextUnit.Internal;

/// <summary>
/// Provides unified display name building functionality for test cases.
/// Consolidates duplicate logic from ClassDataSourceExpander, CombinedDataSourceExpander, and TestDataExpander.
/// </summary>
internal static class DisplayNameBuilder
{
    private static readonly ConcurrentDictionary<Type, IDisplayNameFormatter> _formatterCache = new();
    private static readonly ConcurrentDictionary<Type, byte> _warnedFormatters = new();

    /// <summary>
    /// Builds a display name for a test case using the priority order:
    /// 1. Custom formatter (if formatterType is specified)
    /// 2. Custom template with placeholders (if customDisplayNameTemplate is specified)
    /// 3. Default formatting (MethodName(arg1, arg2, ...))
    /// </summary>
    /// <param name="methodName">The test method name.</param>
    /// <param name="customDisplayNameTemplate">Optional custom display name template with placeholders.</param>
    /// <param name="formatterType">Optional custom formatter type implementing IDisplayNameFormatter.</param>
    /// <param name="testClass">The test class type.</param>
    /// <param name="arguments">The test arguments.</param>
    /// <param name="argumentSetIndex">The index of the argument set (for default naming).</param>
    /// <returns>The formatted display name.</returns>
    public static string Build(
        string methodName,
        string? customDisplayNameTemplate,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? formatterType,
        Type testClass,
        object?[] arguments,
        int argumentSetIndex)
    {
        // Priority 1: Custom formatter
        if (formatterType is not null)
        {
            try
            {
                var formatter = GetFormatter(formatterType);
                var context = new DisplayNameContext
                {
                    MethodName = methodName,
                    TestClass = testClass,
                    Arguments = arguments,
                    ArgumentSetIndex = argumentSetIndex
                };
                return formatter.Format(context);
            }
            catch (InvalidOperationException ex)
            {
                ReportFormatterFailure(formatterType, ex.Message);
                // Fall through to next priority
            }
            catch (TargetInvocationException ex)
            {
                ReportFormatterFailure(formatterType, ex.InnerException?.Message ?? ex.Message);
                // Fall through to next priority
            }
        }

        // Priority 2: Custom template with placeholders
        if (customDisplayNameTemplate is not null)
        {
            return FormatWithPlaceholders(customDisplayNameTemplate, arguments);
        }

        // Priority 3: Default formatting
        if (arguments.Length == 0)
        {
            return methodName;
        }

        var formattedArgs = string.Join(", ", arguments.Select(FormatArgument));
        return $"{methodName}({formattedArgs})";
    }

    private static void ReportFormatterFailure(Type formatterType, string message)
    {
        // Warn once per formatter type: an always-failing formatter is invoked once per expanded data
        // row, so repeating the warning would flood logs and slow discovery on large data sources.
        if (!_warnedFormatters.TryAdd(formatterType, 0))
        {
            return;
        }

        // Route through a best-effort writer so the warning survives Release builds (Debug.WriteLine is
        // compiled out) without a closed/broken error stream turning discovery into a failure.
        Diagnostics.SafeWriteError($"[NextUnit] DisplayNameFormatter '{formatterType.FullName}' failed: {message}");
    }

    /// <summary>
    /// Formats a display name template by replacing placeholders with argument values.
    /// Supports {0}, {1}, etc. for positional arguments.
    /// Uses StringBuilder for efficient in-place replacements.
    /// </summary>
    /// <param name="template">The template string with placeholders.</param>
    /// <param name="arguments">The arguments to substitute.</param>
    /// <returns>The formatted display name.</returns>
    public static string FormatWithPlaceholders(string template, object?[] arguments)
    {
        var sb = new StringBuilder(template);

        for (var i = 0; i < arguments.Length; i++)
        {
            var placeholder = $"{{{i}}}";
            sb.Replace(placeholder, FormatArgument(arguments[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single argument value for display.
    /// Handles null, strings, chars, booleans, enumerables, and general objects.
    /// </summary>
    /// <param name="arg">The argument to format.</param>
    /// <returns>The formatted string representation.</returns>
    public static string FormatArgument(object? arg)
    {
        return arg switch
        {
            null => "null",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => FormatBoolean(b),
            IEnumerable enumerable when arg is not string => FormatEnumerable(enumerable),
            // Data-source rows are formatted on whichever machine runs them, so anything culture
            // sensitive - a double, a decimal, a DateTime - would otherwise report a different name
            // per runner. The invariant culture keeps the reported name a property of the test.
            // The interface annotates ToString as non-null, but user implementations can
            // still return null; fall back to "null" like the general-object arm below.
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "null",
            _ => arg.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Formats a boolean value as lowercase "true" or "false".
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>"true" or "false" in lowercase.</returns>
    public static string FormatBoolean(bool value) => value ? "true" : "false";

    /// <summary>
    /// Gets or creates a cached formatter instance for the specified type.
    /// </summary>
    /// <param name="formatterType">The formatter type.</param>
    /// <returns>The formatter instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type cannot be instantiated or does not implement IDisplayNameFormatter.
    /// </exception>
    public static IDisplayNameFormatter GetFormatter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type formatterType)
    {
        if (_formatterCache.TryGetValue(formatterType, out var cached))
        {
            return cached;
        }

        var instance = Activator.CreateInstance(formatterType)
            ?? throw new InvalidOperationException(
                $"Failed to create display name formatter of type '{formatterType.FullName}'. " +
                "Ensure the type has a public parameterless constructor.");

        var formatter = instance as IDisplayNameFormatter
            ?? throw new InvalidOperationException(
                $"Type '{formatterType.FullName}' must implement IDisplayNameFormatter " +
                "to be used as a display name formatter.");
        return _formatterCache.GetOrAdd(formatterType, formatter);
    }

    private static string FormatEnumerable(IEnumerable enumerable)
    {
        var items = enumerable.Cast<object?>().Take(4).ToList();

        // Use at most three items from the already materialized list
        var displayCount = Math.Min(3, items.Count);
        var formatted = string.Join(", ", items.GetRange(0, displayCount).Select(FormatArgument));

        if (items.Count > 3)
        {
            formatted += ", ...";
        }

        return $"[{formatted}]";
    }
}
