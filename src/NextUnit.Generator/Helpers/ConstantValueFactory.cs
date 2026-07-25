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
    /// <summary>
    /// Identifies a <c>typeof()</c> constant, whose boxed value is a symbol rather than a primitive.
    /// </summary>
    private const string SymbolTypeMarker = "Microsoft.CodeAnalysis.ISymbol";

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
        var typeName = value is ISymbol ? SymbolTypeMarker : value.GetType().FullName ?? string.Empty;
        var text = value is ISymbol symbol ? FormatSymbol(symbol) : FormatScalar(value);

        builder
            .Append('v')
            .Append(typeName.Length).Append(':').Append(typeName)
            .Append(text.Length).Append(':').Append(text);
    }

    private static string FormatSymbol(ISymbol symbol)
    {
        var builder = new StringBuilder();

        if (symbol is ITypeSymbol type)
        {
            AppendTypeKey(builder, type);
        }
        else
        {
            AppendNameAndAssembly(builder, symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), symbol);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends a self-delimiting encoding of a type symbol.
    /// </summary>
    /// <remarks>
    /// Two types can carry the same fully qualified name in different assemblies (extern alias), and
    /// the symbol comparison this key replaces told them apart - including when they only appear as a
    /// type argument, so every part of a constructed type is encoded with its own assembly identity.
    /// </remarks>
    private static void AppendTypeKey(StringBuilder builder, ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                builder.Append("arr").Append(array.Rank).Append(';');
                AppendTypeKey(builder, array.ElementType);
                return;

            case IPointerTypeSymbol pointer:
                builder.Append("ptr;");
                AppendTypeKey(builder, pointer.PointedAtType);
                return;

            case INamedTypeSymbol named:
                builder.Append("nt");
                AppendNameAndAssembly(
                    builder,
                    named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    named);
                builder.Append(named.TypeArguments.Length).Append(';');

                foreach (var typeArgument in named.TypeArguments)
                {
                    AppendTypeKey(builder, typeArgument);
                }

                return;

            default:
                builder.Append("t");
                AppendNameAndAssembly(
                    builder,
                    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    type);
                return;
        }
    }

    private static void AppendNameAndAssembly(StringBuilder builder, string name, ISymbol symbol)
    {
        var assembly = symbol.ContainingAssembly?.Identity.GetDisplayName() ?? string.Empty;

        builder
            .Append(name.Length).Append(':').Append(name)
            .Append(assembly.Length).Append(':').Append(assembly);
    }

    private static string FormatScalar(object value)
    {
        return value switch
        {
            float number => FormatFloat(number),
            double number => FormatDouble(number),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    // "G9"/"G17" instead of the default format: this generator can run in-proc under .NET Framework
    // (VBCSCompiler inside Visual Studio), where the default rounds to 7/15 significant digits and
    // two distinct values would then share a key. NaN and signed zero are normalized because the
    // Equals comparison this key replaces treats all NaNs, and +0 and -0, as equal.
    private static string FormatFloat(float value)
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        return value == 0f ? "0" : value.ToString("G9", CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        return value == 0d ? "0" : value.ToString("G17", CultureInfo.InvariantCulture);
    }
}
