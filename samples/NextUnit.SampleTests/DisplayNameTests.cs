using System.Text.RegularExpressions;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating display name customization features.
/// </summary>
public class DisplayNameTests
{
    // Basic DisplayName usage
    [Test]
    [DisplayName("User authentication should succeed with valid credentials")]
    public void UserAuth_ValidCredentials_Succeeds()
    {
        Assert.True(true);
    }

    // DisplayName with placeholders for parameterized tests
    [Test]
    [DisplayName("Adding {0} + {1} should equal {2}")]
    [Arguments(1, 2, 3)]
    [Arguments(10, 20, 30)]
    [Arguments(-5, 5, 0)]
    public void Add_WithPlaceholders(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    // DisplayName with TestData
    public static IEnumerable<object[]> DivisionCases =>
    [
        [10, 2, 5],
        [100, 10, 10],
        [15, 3, 5]
    ];

    [Test]
    [DisplayName("Dividing {0} by {1} yields {2}")]
    [TestData(nameof(DivisionCases))]
    public void Divide_WithPlaceholders(int dividend, int divisor, int expected)
    {
        Assert.Equal(expected, dividend / divisor);
    }

    // Existing simple tests (unchanged)
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

/// <summary>
/// Custom formatter that converts method names to human-readable format.
/// </summary>
public class HumanReadableFormatter : IDisplayNameFormatter
{
    public string Format(DisplayNameContext context)
    {
        // Convert "UserLogin_ValidCredentials_Succeeds" to "User login valid credentials succeeds"
        var result = Regex.Replace(context.MethodName, "([a-z])([A-Z])", "$1 $2")
                          .Replace("_", " ");

        if (context.Arguments is { Length: > 0 })
        {
            var args = string.Join(", ", context.Arguments.Select(a => a?.ToString() ?? "null"));
            result += $" ({args})";
        }

        return result.ToLowerInvariant();
    }
}

/// <summary>
/// Tests demonstrating DisplayNameFormatter usage.
/// </summary>
[DisplayNameFormatter<HumanReadableFormatter>]
public class FormatterTests
{
    [Test]
    public void UserLogin_ValidCredentials_Succeeds()
    {
        // Display name: "user login valid credentials succeeds"
        Assert.True(true);
    }

    [Test]
    [Arguments(1, 2)]
    public void Add_TwoNumbers_ReturnsSum(int a, int b)
    {
        // Display name: "add two numbers returns sum (1, 2)"
        Assert.Equal(a + b, a + b);
    }

    // Method-level DisplayName overrides class-level formatter
    [Test]
    [DisplayName("This uses explicit DisplayName, not formatter")]
    public void ExplicitDisplayName()
    {
        Assert.True(true);
    }
}
