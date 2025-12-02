using NextUnit;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating parameterized test functionality.
/// </summary>
public class ParameterizedTests
{
    [Test]
    [Arguments(2, 3, 5)]
    [Arguments(1, 1, 2)]
    [Arguments(-1, 1, 0)]
    [Arguments(0, 0, 0)]
    public void Add_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Test]
    [Arguments("hello", 5)]
    [Arguments("world", 5)]
    [Arguments("", 0)]
    public void String_HasCorrectLength(string text, int expectedLength)
    {
        Assert.Equal(expectedLength, text.Length);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void Boolean_WorksCorrectly(bool value)
    {
        Assert.Equal(value, value);
    }

    [Test]
    [Arguments(null, true)]
    [Arguments("test", false)]
    public void Null_IsHandledCorrectly(string? value, bool expectedIsNull)
    {
        var isNull = value == null;
        Assert.Equal(expectedIsNull, isNull);
    }
}
