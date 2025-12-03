namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating rich assertion functionality (M4 - v1.0 features).
/// </summary>
public class RichAssertionTests
{
    // Collection Assertions

    [Test]
    public void Contains_FindsElementInCollection()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        Assert.Contains(3, numbers);
    }

    [Test]
    public void DoesNotContain_VerifiesElementNotInCollection()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        Assert.DoesNotContain(6, numbers);
    }

    [Test]
    public void All_VerifiesAllElementsSatisfyCondition()
    {
        var numbers = new[] { 2, 4, 6, 8, 10 };
        Assert.All(numbers, n => Assert.True(n % 2 == 0));
    }

    [Test]
    public void Single_ReturnsSingleElement()
    {
        var numbers = new[] { 42 };
        var result = Assert.Single(numbers);
        Assert.Equal(42, result);
    }

    [Test]
    public void Empty_VerifiesCollectionIsEmpty()
    {
        var emptyList = new List<int>();
        Assert.Empty(emptyList);
    }

    [Test]
    public void NotEmpty_VerifiesCollectionHasElements()
    {
        var numbers = new[] { 1, 2, 3 };
        Assert.NotEmpty(numbers);
    }

    // String Assertions

    [Test]
    public void StartsWith_VerifiesStringPrefix()
    {
        var text = "Hello, World!";
        Assert.StartsWith("Hello", text);
    }

    [Test]
    public void EndsWith_VerifiesStringSuffix()
    {
        var text = "Hello, World!";
        Assert.EndsWith("World!", text);
    }

    [Test]
    public void Contains_FindsSubstringInString()
    {
        var text = "Hello, World!";
        Assert.Contains("World", text);
    }

    // Numeric Assertions

    [Test]
    public void InRange_VerifiesValueWithinRange()
    {
        var value = 5;
        Assert.InRange(value, 1, 10);
    }

    [Test]
    public void NotInRange_VerifiesValueOutsideRange()
    {
        var value = 15;
        Assert.NotInRange(value, 1, 10);
    }

    [Test]
    public void InRange_WorksWithDoubles()
    {
        var value = 3.14;
        Assert.InRange(value, 3.0, 4.0);
    }

    [Test]
    public void InRange_WorksWithDates()
    {
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var tomorrow = today.AddDays(1);

        Assert.InRange(today, yesterday, tomorrow);
    }

    // Combined Scenarios

    [Test]
    public void CollectionProcessing_ValidatesTransformedData()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        var doubled = numbers.Select(n => n * 2).ToArray();

        Assert.NotEmpty(doubled);
        Assert.Contains(6, doubled);
        Assert.DoesNotContain(5, doubled);
        Assert.All(doubled, n => Assert.InRange(n, 2, 10));
    }

    [Test]
    public void StringManipulation_ValidatesFormatting()
    {
        var name = "NextUnit";
        var formatted = $"Welcome to {name}!";

        Assert.StartsWith("Welcome", formatted);
        Assert.EndsWith("!", formatted);
        Assert.Contains(name, formatted);
    }

    [Test]
    [Arguments(new[] { 1, 2, 3 }, 2)]
    [Arguments(new[] { 10, 20, 30, 40 }, 20)]
    public void ParameterizedCollection_ContainsExpectedElement(int[] collection, int expected)
    {
        Assert.Contains(expected, collection);
        Assert.NotEmpty(collection);
    }

    [Test]
    [Arguments("test@example.com", "@")]
    [Arguments("https://github.com", "://")]
    public void ParameterizedString_ContainsSubstring(string text, string substring)
    {
        Assert.Contains(substring, text);
    }
}
