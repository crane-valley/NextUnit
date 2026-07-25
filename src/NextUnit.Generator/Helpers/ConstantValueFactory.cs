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
        var builder = new StringBuilder();
        AppendEqualityKey(builder, constant);
        return builder.ToString();
    }

    /// <summary>
    /// Appends a self-delimiting encoding of one constant.
    /// </summary>
    /// <remarks>
    /// Every part is length-prefixed and the three kinds start with distinct markers, so the encoding
    /// is injective: no value can produce the key of a different value, which a separator-joined
    /// encoding would allow for text containing the separator.
    /// </remarks>
    private static void AppendEqualityKey(StringBuilder builder, TypedConstant constant)
    {
        if (constant.IsNull)
        {
            builder.Append("n;");
            return;
        }

        if (constant.Kind == TypedConstantKind.Array)
        {
            builder.Append('a').Append(constant.Values.Length).Append(';');

            foreach (var element in constant.Values)
            {
                AppendEqualityKey(builder, element);
            }

            return;
        }

        var value = constant.Value;
        if (value is null)
        {
            builder.Append("n;");
            return;
        }

        // The runtime type is part of the key because the previous matcher compared boxed values with
        // object.Equals, where an int and a long holding the same number are not equal.
        var typeName = value.GetType().FullName ?? string.Empty;
        var text = value is ISymbol symbol
            ? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        builder
            .Append('v')
            .Append(typeName.Length).Append(':').Append(typeName)
            .Append(text.Length).Append(':').Append(text);
    }
}
