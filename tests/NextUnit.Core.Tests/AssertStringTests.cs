namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for the string assertions: StartsWith, EndsWith, and Contains.
/// </summary>
public class AssertStringTests
{
    [Test]
    public void StartsWith_MatchingPrefix_DoesNotThrow()
    {
        Assert.StartsWith("Hello", "Hello world");
    }

    [Test]
    public void StartsWith_NonMatchingPrefix_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.StartsWith("Bye", "Hello world"));
        Assert.Contains("does not start with", ex.Message);
    }

    [Test]
    public void StartsWith_NullActual_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.StartsWith("Hello", null));
    }

    [Test]
    public void StartsWith_NullExpected_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Assert.StartsWith(null!, "value"));
    }

    [Test]
    public void EndsWith_MatchingSuffix_DoesNotThrow()
    {
        Assert.EndsWith("world", "Hello world");
    }

    [Test]
    public void EndsWith_NonMatchingSuffix_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.EndsWith("planet", "Hello world"));
        Assert.Contains("does not end with", ex.Message);
    }

    [Test]
    public void EndsWith_NullActual_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.EndsWith("world", null));
    }

    [Test]
    public void Contains_SubstringPresent_DoesNotThrow()
    {
        Assert.Contains("lo wo", "Hello world");
    }

    [Test]
    public void Contains_SubstringAbsent_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Contains("xyz", "Hello world"));
        Assert.Contains("does not contain expected substring", ex.Message);
    }

    [Test]
    public void Contains_NullActual_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Contains("lo", (string?)null));
    }

    [Test]
    public void Contains_CaseSensitive_Throws()
    {
        // The string Contains overload uses ordinal comparison, so case differences fail.
        Assert.Throws<AssertionFailedException>(() => Assert.Contains("HELLO", "hello world"));
    }
}
