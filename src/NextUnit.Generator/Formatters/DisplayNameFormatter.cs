using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Formatters;

/// <summary>
/// Formats display names for tests.
/// </summary>
internal static class DisplayNameFormatter
{
    /// <summary>
    /// Builds a display name for a parameterized test.
    /// </summary>
    public static string BuildParameterizedDisplayName(string methodName, string? customDisplayName, ImmutableArray<TypedConstant> arguments)
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

            argsBuilder.Append(FormatArgumentForDisplay(arguments[i]));
        }

        argsBuilder.Append(')');
        return argsBuilder.ToString();
    }

    /// <summary>
    /// Formats a display name template with placeholder values.
    /// </summary>
    public static string FormatDisplayNameWithPlaceholders(string template, ImmutableArray<TypedConstant> arguments)
    {
        var result = template;
        for (var i = 0; i < arguments.Length; i++)
        {
            var placeholder = $"{{{i}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, FormatArgumentForDisplay(arguments[i]));
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
            TypedConstantKind.Enum => $"{argument.Type!.Name}.{argument.Value}",
            TypedConstantKind.Type => $"typeof({((ITypeSymbol)argument.Value!).Name})",
            TypedConstantKind.Array => FormatArrayForDisplay(argument),
            _ => argument.Value?.ToString() ?? "null"
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
            float f => f.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
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
