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

    // TestData attribute tests

    /// <summary>
    /// Data source providing test data from a static method.
    /// </summary>
    public static IEnumerable<object[]> MultiplyTestCases()
    {
        yield return new object[] { 2, 3, 6 };
        yield return new object[] { 4, 5, 20 };
        yield return new object[] { 0, 100, 0 };
        yield return new object[] { -1, 5, -5 };
    }

    /// <summary>
    /// Test using [TestData] with a static method data source.
    /// </summary>
    [Test]
    [TestData(nameof(MultiplyTestCases))]
    public void Multiply_ReturnsCorrectProduct(int a, int b, int expected)
    {
        var result = a * b;
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Data source providing test data from a static property.
    /// </summary>
    public static IEnumerable<object[]> DivisionTestCases =>
    [
        [10, 2, 5],
        [100, 10, 10],
        [15, 3, 5]
    ];

    /// <summary>
    /// Test using [TestData] with a static property data source.
    /// </summary>
    [Test]
    [TestData(nameof(DivisionTestCases))]
    public void Divide_ReturnsCorrectQuotient(int a, int b, int expected)
    {
        var result = a / b;
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Test using [TestData] with MemberType to specify an external data source.
    /// </summary>
    [Test]
    [TestData(nameof(ExternalTestDataSource.SubtractionTestCases), MemberType = typeof(ExternalTestDataSource))]
    public void Subtract_ReturnsCorrectDifference(int a, int b, int expected)
    {
        var result = a - b;
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Test using multiple [TestData] attributes.
    /// </summary>
    [Test]
    [TestData(nameof(PositiveNumberCases))]
    [TestData(nameof(NegativeNumberCases))]
    public void Abs_ReturnsCorrectAbsoluteValue(int value, int expected)
    {
        var result = Math.Abs(value);
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Data source for positive numbers.
    /// </summary>
    public static IEnumerable<object[]> PositiveNumberCases =>
    [
        [5, 5],
        [10, 10],
        [0, 0]
    ];

    /// <summary>
    /// Data source for negative numbers.
    /// </summary>
    public static IEnumerable<object[]> NegativeNumberCases =>
    [
        [-5, 5],
        [-10, 10],
        [-1, 1]
    ];
}

/// <summary>
/// External class providing test data for TestData attribute tests.
/// </summary>
public static class ExternalTestDataSource
{
    /// <summary>
    /// Test data for subtraction tests.
    /// </summary>
    public static IEnumerable<object[]> SubtractionTestCases()
    {
        yield return new object[] { 10, 5, 5 };
        yield return new object[] { 20, 8, 12 };
        yield return new object[] { 5, 10, -5 };
    }
}
