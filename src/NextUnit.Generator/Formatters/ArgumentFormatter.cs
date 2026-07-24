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
            float f when float.IsNaN(f) => "global::System.Single.NaN",
            float f when float.IsPositiveInfinity(f) => "global::System.Single.PositiveInfinity",
            float f when float.IsNegativeInfinity(f) => "global::System.Single.NegativeInfinity",
            // "G9"/"G17" (not the bare default, and not "R") guarantee a round-trippable
            // literal on every runtime this netstandard2.0 generator can execute under.
            // .NET Core 3.0+ hosts already format shortest-round-trippable by default, but
            // this generator can also run in-proc under .NET Framework (e.g. VBCSCompiler
            // inside Visual Studio), where the bare ToString() falls back to 15/7
            // significant digits. At that precision, float.MaxValue/double.MaxValue (and
            // MinValue) round to a string that no longer fits the type's range, so the
            // emitted literal fails to compile with CS0594. "R" is avoided because it has
            // its own documented round-trip bugs on .NET Framework for float/double.
            float f => $"{f.ToString("G9", CultureInfo.InvariantCulture)}f",
            double d when double.IsNaN(d) => "global::System.Double.NaN",
            double d when double.IsPositiveInfinity(d) => "global::System.Double.PositiveInfinity",
            double d when double.IsNegativeInfinity(d) => "global::System.Double.NegativeInfinity",
            double d => $"{d.ToString("G17", CultureInfo.InvariantCulture)}d",
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
