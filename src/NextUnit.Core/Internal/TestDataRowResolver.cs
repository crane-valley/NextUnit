using System.Collections;
using System.Runtime.CompilerServices;

namespace NextUnit.Internal;

internal readonly record struct ResolvedTestDataRow(
    object?[] Arguments,
    string? DisplayName,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    string? SkipReason);

internal static class TestDataRowResolver
{
    public static ResolvedTestDataRow Resolve(object? dataRow)
    {
        if (dataRow is not ITestDataRow typedRow)
        {
            return new ResolvedTestDataRow(
                ConvertToObjectArray(dataRow),
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                null);
        }

        return new ResolvedTestDataRow(
            ConvertTypedDataToObjectArray(typedRow.Data),
            typedRow.DisplayName,
            typedRow.Categories,
            typedRow.Tags,
            typedRow.SkipReason);
    }

    public static IReadOnlyList<string> MergeLabels(
        IReadOnlyList<string> testLabels,
        IReadOnlyList<string> rowLabels)
    {
        if (rowLabels.Count == 0)
        {
            return testLabels;
        }

        return testLabels
            .Concat(rowLabels)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static object?[] ConvertToObjectArray(object? data)
    {
        return data switch
        {
            null => [],
            object?[] array => array,
            ITuple tuple => ConvertTuple(tuple),
            string text => [text],
            IEnumerable enumerable => enumerable.Cast<object?>().ToArray(),
            _ => [data]
        };
    }

    private static object?[] ConvertTypedDataToObjectArray(object? data)
    {
        return data switch
        {
            object?[] array => array,
            ITuple tuple => ConvertTuple(tuple),
            _ => [data]
        };
    }

    private static object?[] ConvertTuple(ITuple tuple)
    {
        var arguments = new object?[tuple.Length];
        for (var i = 0; i < tuple.Length; i++)
        {
            arguments[i] = tuple[i];
        }

        return arguments;
    }
}
