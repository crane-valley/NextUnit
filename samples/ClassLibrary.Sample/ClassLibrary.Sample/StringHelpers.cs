namespace ClassLibrary.Sample;

/// <summary>
/// Helper class for string manipulation operations.
/// </summary>
public static class StringHelpers
{
    /// <summary>
    /// Reverses a string.
    /// </summary>
    public static string Reverse(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        char[] chars = input.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>
    /// Checks if a string is a palindrome.
    /// </summary>
    public static bool IsPalindrome(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return true;
        }

        string normalized = input.Replace(" ", "").ToLowerInvariant();
        return normalized == Reverse(normalized);
    }

    /// <summary>
    /// Counts the number of words in a string.
    /// </summary>
    public static int CountWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        return input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Truncates a string to a specified length.
    /// </summary>
    public static string Truncate(string input, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
        {
            return input;
        }

        return input.Substring(0, maxLength) + suffix;
    }
}
