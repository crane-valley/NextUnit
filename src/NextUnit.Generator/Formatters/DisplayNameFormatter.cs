using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Formatters;

/// <summary>
/// Formats display names for tests.
/// </summary>
internal static class DisplayNameFormatter
{
    /// <summary>
    /// Builds a display name for a matrix test, including parameter names.
    /// </summary>
    public static string BuildMatrixDisplayName(
        string methodName,
        string? customDisplayName,
        EquatableArray<MatrixParameterDescriptor> matrixParameters,
        EquatableArray<ConstantValue> combination)
    {
        if (customDisplayName is not null)
        {
            return FormatDisplayNameWithPlaceholders(customDisplayName, combination);
        }

        var builder = new StringBuilder();
        builder.Append(methodName);
        builder.Append('(');

        for (var i = 0; i < combination.Length && i < matrixParameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(matrixParameters[i].ParameterName);
            builder.Append(": ");
            builder.Append(combination[i].DisplayLiteral);
        }

        builder.Append(')');
        return builder.ToString();
    }

    /// <summary>
    /// Builds a display name for a parameterized test.
    /// </summary>
    public static string BuildParameterizedDisplayName(string methodName, string? customDisplayName, EquatableArray<ConstantValue> arguments)
    {
        if (customDisplayName is not null)
        {
            return FormatDisplayNameWithPlaceholders(customDisplayName, arguments);
        }

        var argsBuilder = new StringBuilder();
        argsBuilder.Append(methodName);
        argsBuilder.Append('(');

        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                argsBuilder.Append(", ");
            }

            argsBuilder.Append(arguments[i].DisplayLiteral);
        }

        argsBuilder.Append(')');
        return argsBuilder.ToString();
    }

    /// <summary>
    /// Formats a display name template with placeholder values.
    /// </summary>
    public static string FormatDisplayNameWithPlaceholders(string template, EquatableArray<ConstantValue> arguments)
    {
        var result = template;
        for (var i = 0; i < arguments.Length; i++)
        {
            var placeholder = $"{{{i}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, arguments[i].DisplayLiteral);
            }
        }
        return result;
    }

    /// <summary>
    /// Formats an argument for display in test names.
    /// </summary>
    public static string FormatArgumentForDisplay(TypedConstant argument)
    {
        if (argument.IsNull)
        {
            return "null";
        }

        return argument.Kind switch
        {
            TypedConstantKind.Primitive => FormatPrimitiveForDisplay(argument.Value!),
            // An enum constant carries its boxed underlying value, which is integral and so goes
            // through the same invariant path as any other number.
            TypedConstantKind.Enum => $"{argument.Type!.Name}.{FormatPrimitiveForDisplay(argument.Value!)}",
            TypedConstantKind.Type => $"typeof({((ITypeSymbol)argument.Value!).Name})",
            TypedConstantKind.Array => FormatArrayForDisplay(argument),
            _ => argument.Value is null ? "null" : FormatPrimitiveForDisplay(argument.Value)
        };
    }

    /// <summary>
    /// Formats a primitive value for display.
    /// </summary>
    public static string FormatPrimitiveForDisplay(object value)
    {
        return value switch
        {
            string str => $"\"{str}\"",
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            // Every remaining primitive constant is IFormattable, and this name is baked into the
            // generated registry: a culture-sensitive ToString would make the same source produce a
            // different display name per build machine - de-DE's decimal comma, sv-SE's U+2212
            // negative sign - and therefore break name-based filters written against it.
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Formats an array for display.
    /// </summary>
    public static string FormatArrayForDisplay(TypedConstant argument)
    {
        var elements = argument.Values;

        if (elements.IsEmpty)
        {
            return "[]";
        }

        var builder = new StringBuilder();
        builder.Append('[');

        for (var i = 0; i < Math.Min(elements.Length, 3); i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(FormatArgumentForDisplay(elements[i]));
        }

        if (elements.Length > 3)
        {
            builder.Append(", ...");
        }

        builder.Append(']');
        return builder.ToString();
    }
}
