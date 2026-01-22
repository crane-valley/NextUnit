using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Helpers;

namespace NextUnit.Generator.Formatters;

/// <summary>
/// Formats argument values for code generation.
/// </summary>
internal static class ArgumentFormatter
{
    /// <summary>
    /// Formats an argument value for use in generated code.
    /// </summary>
    public static string FormatArgumentValue(TypedConstant argument, ITypeSymbol? targetType)
    {
        if (argument.IsNull)
        {
            if (targetType != null && targetType.IsValueType)
            {
                return $"default({targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
            }
            return "null";
        }

        return argument.Kind switch
        {
            TypedConstantKind.Primitive => FormatPrimitiveValue(argument.Value!, argument.Type!),
            TypedConstantKind.Enum => $"({argument.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){argument.Value}",
            TypedConstantKind.Type => $"typeof({((ITypeSymbol)argument.Value!).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})",
            TypedConstantKind.Array => FormatArrayValue(argument),
            _ => "null"
        };
    }

    /// <summary>
    /// Formats a primitive value for use in generated code.
    /// </summary>
    public static string FormatPrimitiveValue(object value, ITypeSymbol type)
    {
        return value switch
        {
            string str => AttributeHelper.ToLiteral(str),
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            byte or sbyte or short or ushort or int or uint => value.ToString()!,
            long l => $"{l}L",
            ulong ul => $"{ul}UL",
            float f => $"{f.ToString(CultureInfo.InvariantCulture)}f",
            double d => $"{d.ToString(CultureInfo.InvariantCulture)}d",
            decimal m => $"{m.ToString(CultureInfo.InvariantCulture)}m",
            _ => value.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Formats an array value for use in generated code.
    /// </summary>
    public static string FormatArrayValue(TypedConstant argument)
    {
        var elementType = ((IArrayTypeSymbol)argument.Type!).ElementType;
        var elements = argument.Values;

        if (elements.IsEmpty)
        {
            return $"global::System.Array.Empty<{elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()";
        }

        var builder = new StringBuilder();
        builder.Append($"new {elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}[] {{ ");

        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(FormatArgumentValue(elements[i], elementType));
        }

        builder.Append(" }");
        return builder.ToString();
    }

    /// <summary>
    /// Builds an arguments literal for object array initialization.
    /// </summary>
    public static string BuildArgumentsLiteral(ImmutableArray<TypedConstant> arguments)
    {
        if (arguments.IsEmpty)
        {
            return "global::System.Array.Empty<object?>()";
        }

        var builder = new StringBuilder();
        builder.Append("new object?[] { ");

        for (var i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var arg = arguments[i];
            if (arg.IsNull)
            {
                builder.Append("null");
            }
            else
            {
                builder.Append(FormatArgumentValue(arg, null));
            }
        }

        builder.Append(" }");
        return builder.ToString();
    }
}
