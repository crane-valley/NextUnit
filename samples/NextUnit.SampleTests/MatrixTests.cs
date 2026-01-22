namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates the [Matrix] attribute for Cartesian product test case generation.
/// </summary>
public class MatrixTests
{
    /// <summary>
    /// Basic matrix test - generates 6 test cases (3 x 2 = 6 combinations).
    /// </summary>
    [Test]
    public void TestAddition(
        [Matrix(1, 2, 3)] int a,
        [Matrix(10, 20)] int b)
    {
        var result = a + b;
        Assert.True(result > 0);
        Assert.True(result <= 23);
    }

    /// <summary>
    /// Matrix test with string values.
    /// </summary>
    [Test]
    public void TestStringConcatenation(
        [Matrix("Hello", "Hi")] string greeting,
        [Matrix("World", "There")] string subject)
    {
        var result = $"{greeting}, {subject}!";
        Assert.NotNull(result);
        Assert.True(result.Contains(","));
    }

    /// <summary>
    /// Matrix test with exclusion - generates 3 test cases (excludes a=1, b=10).
    /// </summary>
    [Test]
    [MatrixExclusion(1, 10)]
    public void TestWithExclusion(
        [Matrix(1, 2)] int a,
        [Matrix(10, 20)] int b)
    {
        // This combination (a=1, b=10) should be excluded
        Assert.False(a == 1 && b == 10, "This combination should have been excluded");
    }

    /// <summary>
    /// Matrix test with multiple exclusions.
    /// </summary>
    [Test]
    [MatrixExclusion(1, 10)]
    [MatrixExclusion(2, 20)]
    public void TestWithMultipleExclusions(
        [Matrix(1, 2)] int a,
        [Matrix(10, 20)] int b)
    {
        // These combinations should be excluded
        Assert.False(a == 1 && b == 10, "Combination (1, 10) should have been excluded");
        Assert.False(a == 2 && b == 20, "Combination (2, 20) should have been excluded");
    }

    /// <summary>
    /// Matrix test with boolean values.
    /// </summary>
    [Test]
    public void TestBooleanMatrix(
        [Matrix(true, false)] bool flag1,
        [Matrix(true, false)] bool flag2)
    {
        // All 4 combinations: (true, true), (true, false), (false, true), (false, false)
        var xor = flag1 ^ flag2;
        Assert.True(xor == (flag1 != flag2));
    }

    /// <summary>
    /// Matrix test with nullable values.
    /// </summary>
    [Test]
    public void TestNullableMatrix(
        [Matrix(null, "value")] string? input,
        [Matrix(0, 1)] int multiplier)
    {
        var result = input is null ? "default" : input;
        Assert.NotNull(result);
    }

    /// <summary>
    /// Matrix test with custom display name.
    /// </summary>
    [Test]
    [DisplayName("Add {0} + {1}")]
    public void TestCustomDisplayName(
        [Matrix(5, 10)] int x,
        [Matrix(3, 7)] int y)
    {
        Assert.True(x + y > 0);
    }

    /// <summary>
    /// Matrix test combined with [Repeat].
    /// </summary>
    [Test]
    [Repeat(2)]
    public void TestMatrixWithRepeat(
        [Matrix(1, 2)] int value,
        [Matrix("a", "b")] string label)
    {
        // Each of the 4 matrix combinations runs 2 times = 8 total test cases
        Assert.True(value > 0);
        Assert.NotNull(label);
    }

    /// <summary>
    /// Matrix test with three parameters.
    /// </summary>
    [Test]
    public void TestThreeParameterMatrix(
        [Matrix(1, 2)] int a,
        [Matrix(10, 20)] int b,
        [Matrix(100, 200)] int c)
    {
        // 2 x 2 x 2 = 8 combinations
        var sum = a + b + c;
        Assert.True(sum >= 111);
        Assert.True(sum <= 222);
    }

    /// <summary>
    /// Matrix test with enum values.
    /// </summary>
    [Test]
    public void TestEnumMatrix(
        [Matrix(DayOfWeek.Monday, DayOfWeek.Friday)] DayOfWeek day,
        [Matrix(true, false)] bool isWorkday)
    {
        if (day == DayOfWeek.Monday || day == DayOfWeek.Friday)
        {
            // Both are weekdays in most contexts
            Assert.True(day != DayOfWeek.Saturday && day != DayOfWeek.Sunday);
        }
    }
}
