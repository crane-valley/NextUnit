using NextUnit;

namespace ClassLibrary.Sample.Tests;

/// <summary>
/// Tests for the StringHelpers class, demonstrating string manipulation tests.
/// </summary>
public class StringHelpersTests
{
    [Test]
    public void Reverse_SimpleString_ReturnsReversed()
    {
        string result = StringHelpers.Reverse("hello");
        Assert.Equal("olleh", result);
    }

    [Test]
    public void Reverse_EmptyString_ReturnsEmpty()
    {
        string result = StringHelpers.Reverse("");
        Assert.Equal("", result);
    }

    [Test]
    public void Reverse_NullString_ReturnsNull()
    {
        string? result = StringHelpers.Reverse(null!);
        Assert.Null(result);
    }

    [Test]
    public void IsPalindrome_ValidPalindrome_ReturnsTrue()
    {
        bool result = StringHelpers.IsPalindrome("racecar");
        Assert.True(result);
    }

    [Test]
    public void IsPalindrome_PalindromeWithSpaces_ReturnsTrue()
    {
        bool result = StringHelpers.IsPalindrome("race car");
        Assert.True(result);
    }

    [Test]
    public void IsPalindrome_NotAPalindrome_ReturnsFalse()
    {
        bool result = StringHelpers.IsPalindrome("hello");
        Assert.False(result);
    }

    [Test]
    public void IsPalindrome_EmptyString_ReturnsTrue()
    {
        bool result = StringHelpers.IsPalindrome("");
        Assert.True(result);
    }

    [Test]
    public void CountWords_SingleWord_ReturnsOne()
    {
        int result = StringHelpers.CountWords("hello");
        Assert.Equal(1, result);
    }

    [Test]
    public void CountWords_MultipleWords_ReturnsCorrectCount()
    {
        int result = StringHelpers.CountWords("hello world from NextUnit");
        Assert.Equal(4, result);
    }

    [Test]
    public void CountWords_ExtraSpaces_IgnoresExtraSpaces()
    {
        int result = StringHelpers.CountWords("hello  world   test");
        Assert.Equal(3, result);
    }

    [Test]
    public void CountWords_EmptyString_ReturnsZero()
    {
        int result = StringHelpers.CountWords("");
        Assert.Equal(0, result);
    }

    [Test]
    public void Truncate_LongString_TruncatesWithSuffix()
    {
        string result = StringHelpers.Truncate("This is a very long string", 10);
        Assert.Equal("This is a ...", result);
    }

    [Test]
    public void Truncate_ShortString_ReturnsOriginal()
    {
        string input = "short";
        string result = StringHelpers.Truncate(input, 10);
        Assert.Equal(input, result);
    }

    [Test]
    public void Truncate_WithCustomSuffix_UsesCustomSuffix()
    {
        string result = StringHelpers.Truncate("This is a very long string", 10, "[...]");
        Assert.Equal("This is a [...]", result);
    }

    /// <summary>
    /// Demonstrates parameterized tests with TestData attribute.
    /// </summary>
    [Test]
    [TestData(nameof(ReverseTestCases))]
    public void Reverse_VariousInputs_ReturnsExpectedOutput(string input, string expected)
    {
        string result = StringHelpers.Reverse(input);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Demonstrates multiple test cases for palindromes.
    /// </summary>
    [Test]
    [TestData(nameof(PalindromeTestCases))]
    public void IsPalindrome_VariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        bool result = StringHelpers.IsPalindrome(input);
        Assert.Equal(expected, result);
    }

    // Test data providers
    public static IEnumerable<object[]> ReverseTestCases()
    {
        yield return new object[] { "hello", "olleh" };
        yield return new object[] { "NextUnit", "tinUtxeN" };
        yield return new object[] { "a", "a" };
    }

    public static IEnumerable<object[]> PalindromeTestCases()
    {
        yield return new object[] { "racecar", true };
        yield return new object[] { "hello", false };
        yield return new object[] { "A man a plan a canal Panama", true };
        yield return new object[] { "", true };
    }
}
