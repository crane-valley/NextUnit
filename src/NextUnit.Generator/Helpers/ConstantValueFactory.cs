using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using NextUnit.Generator.Formatters;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Converts Roslyn <see cref="TypedConstant"/> values into <see cref="ConstantValue"/> models.
/// </summary>
/// <remarks>
/// The conversion happens while the semantic model is still at hand, which is what keeps the
/// pipeline models free of symbols.
/// </remarks>
internal static class ConstantValueFactory
{
    public static EquatableArray<ConstantValue> CreateRange(ImmutableArray<TypedConstant> constants)
    {
        if (constants.IsDefaultOrEmpty)
        {
            return EquatableArray<ConstantValue>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ConstantValue>(constants.Length);

        foreach (var constant in constants)
        {
            builder.Add(Create(constant));
        }

        return new EquatableArray<ConstantValue>(builder.ToImmutable());
    }

    public static ConstantValue Create(TypedConstant constant) =>
        new(
            ArgumentFormatter.FormatArgumentValue(constant, targetType: null),
            DisplayNameFormatter.FormatArgumentForDisplay(constant),
            BuildEqualityKey(constant),
            constant.IsNull);

    private static string BuildEqualityKey(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            var builder = new StringBuilder("[");

            for (var i = 0; i < constant.Values.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(BuildEqualityKey(constant.Values[i]));
            }

            builder.Append(']');
            return builder.ToString();
        }

        var value = constant.Value;
        if (value is null)
        {
            return "null";
        }

        // The runtime type is part of the key because the previous matcher compared boxed values with
        // object.Equals, where an int and a long holding the same number are not equal.
        var text = value is ISymbol symbol
            ? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return $"{value.GetType().FullName}:{text}";
    }
}
