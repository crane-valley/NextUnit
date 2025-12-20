using System.Text;

namespace NextUnit.Internal;

/// <summary>
/// Provides utilities for generating rich, developer-friendly assertion failure messages.
/// </summary>
internal static class AssertionMessageFormatter
{
    private const int ContextBefore = 20;
    private const int ContextAfter = 40;
    private const int MaxDisplayedDifferences = 10;
    private const int MaxDisplayedProperties = 5;
    private const int MaxDisplayedCollectionItems = 10;
    private const int MaxShortStringLength = 100;

    /// <summary>
    /// Formats a string comparison failure with visual diff highlighting.
    /// </summary>
    public static string FormatStringDifference(string? expected, string? actual)
    {
        if (expected == null && actual == null)
        {
            return "Both strings are null";
        }

        if (expected == null)
        {
            return $"Expected: <null>; Actual: \"{actual}\"";
        }

        if (actual == null)
        {
            return $"Expected: \"{expected}\"; Actual: <null>";
        }

        // If strings are equal, this shouldn't be called, but handle it anyway
        if (expected == actual)
        {
            return $"Strings are equal: \"{expected}\"";
        }

        var sb = new StringBuilder();
        sb.AppendLine("String assertion failed:");
        sb.AppendLine($"Expected length: {expected.Length}");
        sb.AppendLine($"Actual length:   {actual.Length}");

        // Find first difference
        int firstDiff = FindFirstDifference(expected, actual);
        if (firstDiff >= 0)
        {
            sb.AppendLine($"First difference at index {firstDiff}:");

            // Show context around the difference
            int contextStart = Math.Max(0, firstDiff - ContextBefore);
            int contextEnd = Math.Min(Math.Max(expected.Length, actual.Length), firstDiff + ContextAfter);

            if (contextStart > 0)
            {
                sb.Append("...");
            }

            sb.AppendLine();
            sb.Append("Expected: ");
            AppendStringWithHighlight(sb, expected, contextStart, contextEnd, firstDiff);
            sb.AppendLine();

            sb.Append("Actual:   ");
            AppendStringWithHighlight(sb, actual, contextStart, contextEnd, firstDiff);
            sb.AppendLine();

            if (contextEnd < Math.Max(expected.Length, actual.Length))
            {
                sb.AppendLine("...");
            }
        }

        // Show the full strings if they're short
        if (expected.Length <= MaxShortStringLength && actual.Length <= MaxShortStringLength)
        {
            sb.AppendLine();
            sb.AppendLine($"Expected: \"{expected}\"");
            sb.AppendLine($"Actual:   \"{actual}\"");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a collection comparison failure with detailed item-by-item comparison.
    /// </summary>
    public static string FormatCollectionDifference<T>(IEnumerable<T>? expected, IEnumerable<T>? actual)
    {
        if (expected == null && actual == null)
        {
            return "Both collections are null";
        }

        if (expected == null)
        {
            return "Expected: <null>; Actual: <non-null collection>";
        }

        if (actual == null)
        {
            return "Expected: <non-null collection>; Actual: <null>";
        }

        var expectedList = expected.ToList();
        var actualList = actual.ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Collection assertion failed:");
        sb.AppendLine($"Expected count: {expectedList.Count}");
        sb.AppendLine($"Actual count:   {actualList.Count}");

        // Find differences
        var maxCount = Math.Max(expectedList.Count, actualList.Count);
        var differences = new List<string>();

        for (int i = 0; i < maxCount; i++)
        {
            if (i >= expectedList.Count)
            {
                differences.Add($"  [{i}]: MISSING (actual has extra item: {FormatItem(actualList[i])})");
            }
            else if (i >= actualList.Count)
            {
                differences.Add($"  [{i}]: {FormatItem(expectedList[i])} (missing in actual)");
            }
            else if (!Equals(expectedList[i], actualList[i]))
            {
                differences.Add($"  [{i}]: Expected {FormatItem(expectedList[i])}, Actual {FormatItem(actualList[i])}");
            }
        }

        if (differences.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Differences:");
            foreach (var diff in differences.Take(MaxDisplayedDifferences))
            {
                sb.AppendLine(diff);
            }

            if (differences.Count > MaxDisplayedDifferences)
            {
                sb.AppendLine($"  ... and {differences.Count - MaxDisplayedDifferences} more differences");
            }
        }

        // Show full collections if they're small
        if (expectedList.Count <= MaxDisplayedCollectionItems && actualList.Count <= MaxDisplayedCollectionItems)
        {
            sb.AppendLine();
            sb.AppendLine("Expected: [" + string.Join(", ", expectedList.Select(FormatItem)) + "]");
            sb.AppendLine("Actual:   [" + string.Join(", ", actualList.Select(FormatItem)) + "]");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats a complex object comparison failure with JSON-like output.
    /// </summary>
    public static string FormatObjectDifference<T>(T? expected, T? actual)
    {
        if (expected == null && actual == null)
        {
            return "Both objects are null";
        }

        if (expected == null)
        {
            return $"Expected: <null>; Actual: {FormatObject(actual)}";
        }

        if (actual == null)
        {
            return $"Expected: {FormatObject(expected)}; Actual: <null>";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Object assertion failed:");
        sb.AppendLine($"Expected type: {expected.GetType().Name}");
        sb.AppendLine($"Actual type:   {actual.GetType().Name}");
        sb.AppendLine();
        sb.AppendLine($"Expected: {FormatObject(expected)}");
        sb.AppendLine($"Actual:   {FormatObject(actual)}");

        return sb.ToString().TrimEnd();
    }

    private static int FindFirstDifference(string s1, string s2)
    {
        int minLength = Math.Min(s1.Length, s2.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (s1[i] != s2[i])
            {
                return i;
            }
        }

        // If one string is a prefix of the other
        if (s1.Length != s2.Length)
        {
            return minLength;
        }

        return -1; // Strings are equal
    }

    private static void AppendStringWithHighlight(StringBuilder sb, string str, int start, int end, int highlightIndex)
    {
        int actualEnd = Math.Min(end, str.Length);

        if (start < str.Length)
        {
            // Before highlight
            if (highlightIndex > start && highlightIndex <= actualEnd)
            {
                sb.Append(EscapeString(str.Substring(start, highlightIndex - start)));

                // Highlight character
                if (highlightIndex < str.Length)
                {
                    sb.Append($"[{EscapeChar(str[highlightIndex])}]");

                    // After highlight
                    if (highlightIndex + 1 < actualEnd)
                    {
                        sb.Append(EscapeString(str.Substring(highlightIndex + 1, actualEnd - highlightIndex - 1)));
                    }
                }
                else
                {
                    sb.Append("[END]");
                }
            }
            else
            {
                sb.Append(EscapeString(str.Substring(start, actualEnd - start)));
            }
        }
        else if (highlightIndex == str.Length)
        {
            sb.Append("[END]");
        }
    }

    private static string EscapeString(string str)
    {
        return str
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\"", "\\\"");
    }

    private static string EscapeChar(char c)
    {
        return c switch
        {
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            '\"' => "\\\"",
            _ => c.ToString()
        };
    }

    private static string FormatItem<T>(T? item)
    {
        if (item == null)
        {
            return "<null>";
        }

        if (item is string str)
        {
            return $"\"{EscapeString(str)}\"";
        }

        return item.ToString() ?? "<null>";
    }

    private static string FormatObject<T>(T? obj)
    {
        if (obj == null)
        {
            return "<null>";
        }

        var type = obj.GetType();

        // Handle common types with better formatting
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
        {
            return obj.ToString() ?? "<null>";
        }

        // For complex objects, show type and properties
        var sb = new StringBuilder();
        sb.Append(type.Name);
        sb.Append(" { ");

        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var propertyValues = new List<string>();

        foreach (var prop in properties.Take(MaxDisplayedProperties))
        {
            try
            {
                var value = prop.GetValue(obj);
                propertyValues.Add($"{prop.Name} = {FormatItem(value)}");
            }
            catch (Exception ex) when (IsNonCriticalException(ex))
            {
                propertyValues.Add($"{prop.Name} = <error>");
            }
        }

        sb.Append(string.Join(", ", propertyValues));

        if (properties.Length > MaxDisplayedProperties)
        {
            sb.Append($", ... ({properties.Length - MaxDisplayedProperties} more)");
        }

        sb.Append(" }");
        return sb.ToString();
    }

    private static bool IsNonCriticalException(Exception ex)
    {
        return ex is not OutOfMemoryException
               and not StackOverflowException
               and not ThreadAbortException;
    }
}
