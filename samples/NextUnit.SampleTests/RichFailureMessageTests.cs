namespace NextUnit.Tests;

/// <summary>
/// Tests to verify rich failure messages are working correctly.
/// These tests are designed to FAIL to demonstrate the improved error messages.
/// </summary>
public class RichFailureMessageTests
{
    [Test]
    [Skip("Intentionally failing test to demonstrate rich string diff")]
    public void StringDiff_ShowsRichMessage()
    {
        var expected = "The quick brown fox jumps over the lazy dog";
        var actual = "The quick brown cat jumps over the lazy dog";

        Assert.Equal(expected, actual);
    }

    [Test]
    [Skip("Intentionally failing test to demonstrate rich collection diff")]
    public void CollectionDiff_ShowsRichMessage()
    {
        var expected = new[] { 1, 2, 3, 4, 5 };
        var actual = new[] { 1, 2, 99, 4, 5 };

        Assert.Equal(expected, actual);
    }

    [Test]
    [Skip("Intentionally failing test to demonstrate multi-line string diff")]
    public void MultiLineStringDiff_ShowsRichMessage()
    {
        var expected = "Line 1\nLine 2\nLine 3\nLine 4";
        var actual = "Line 1\nLine TWO\nLine 3\nLine 4";

        Assert.Equal(expected, actual);
    }

    [Test]
    [Skip("Intentionally failing test to demonstrate collection length diff")]
    public void CollectionLengthDiff_ShowsRichMessage()
    {
        var expected = new[] { "apple", "banana", "cherry" };
        var actual = new[] { "apple", "banana", "cherry", "date", "elderberry" };

        Assert.Equal(expected, actual);
    }

    [Test]
    public void SuccessfulStringComparison_DoesNotShowDiff()
    {
        var expected = "Hello, World!";
        var actual = "Hello, World!";

        Assert.Equal(expected, actual);
    }

    [Test]
    public void SuccessfulCollectionComparison_DoesNotShowDiff()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new[] { 1, 2, 3 };

        Assert.Equal(expected, actual);
    }
}
