namespace NextUnit.SampleTests;

/// <summary>
/// Simple tests to verify display names are correctly formatted.
/// </summary>
public class DisplayNameTests
{
    [Test]
    [Arguments(1, 2)]
    [Arguments(10, 20)]
    public void SimpleNumbers(int a, int b)
    {
        Assert.True(a < b);
    }

    [Test]
    [Arguments("hello")]
    [Arguments("world")]
    public void SimpleStrings(string text)
    {
        Assert.NotNull(text);
    }
}
