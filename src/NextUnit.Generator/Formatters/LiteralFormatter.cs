using System.Globalization;
using NextUnit.Generator.Helpers;

namespace NextUnit.Generator.Formatters;

/// <summary>
/// Formats primitive values as the C# literals the generated registry expects.
/// </summary>
internal static class LiteralFormatter
{
    /// <summary>
    /// Formats a boolean as a C# keyword literal.
    /// </summary>
    public static string Bool(bool value) => value ? "true" : "false";

    /// <summary>
    /// Formats an integer using the invariant culture, so the emitted source does not depend on
    /// the machine that compiled it.
    /// </summary>
    public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an optional integer, emitting <c>null</c> when absent.
    /// </summary>
    public static string NullableInt(int? value) => value is int number ? Int(number) : "null";

    /// <summary>
    /// Formats an optional string as a quoted literal, emitting <c>null</c> when absent.
    /// </summary>
    public static string NullableString(string? value) =>
        value is not null ? AttributeHelper.ToLiteral(value) : "null";

    /// <summary>
    /// Formats an optional type name as a <c>typeof</c> expression, emitting <c>null</c> when absent.
    /// </summary>
    public static string NullableTypeof(string? typeName) =>
        typeName is not null ? $"typeof({typeName})" : "null";
}
