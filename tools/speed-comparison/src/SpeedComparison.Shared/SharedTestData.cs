namespace SpeedComparison.Shared;

/// <summary>
/// Shared test data used across all test framework implementations.
/// This ensures fair comparison by using identical test inputs.
/// </summary>
public static class SharedTestData
{
    /// <summary>
    /// Simple integer pairs for addition tests
    /// </summary>
    public static IEnumerable<object[]> AdditionTestCases()
    {
        yield return new object[] { 1, 2, 3 };
        yield return new object[] { 5, 7, 12 };
        yield return new object[] { -1, 1, 0 };
        yield return new object[] { 0, 0, 0 };
        yield return new object[] { 100, 200, 300 };
        yield return new object[] { -50, -50, -100 };
        yield return new object[] { 42, 58, 100 };
        yield return new object[] { 999, 1, 1000 };
        yield return new object[] { -100, 100, 0 };
        yield return new object[] { 7, 3, 10 };
    }

    /// <summary>
    /// String length test cases
    /// </summary>
    public static IEnumerable<object[]> StringLengthTestCases()
    {
        yield return new object[] { "hello", 5 };
        yield return new object[] { "world", 5 };
        yield return new object[] { "", 0 };
        yield return new object[] { "a", 1 };
        yield return new object[] { "test case", 9 };
        yield return new object[] { "benchmark", 9 };
        yield return new object[] { "NextUnit", 8 };
        yield return new object[] { "x", 1 };
        yield return new object[] { "testing framework", 17 };
        yield return new object[] { "ab", 2 };
    }

    /// <summary>
    /// Numeric range test cases
    /// </summary>
    public static IEnumerable<object[]> RangeTestCases()
    {
        yield return new object[] { 5, 1, 10 };
        yield return new object[] { 50, 0, 100 };
        yield return new object[] { 0, -10, 10 };
        yield return new object[] { 99, 90, 100 };
        yield return new object[] { -5, -10, 0 };
        yield return new object[] { 25, 20, 30 };
        yield return new object[] { 1, 1, 1 };
        yield return new object[] { 75, 50, 100 };
        yield return new object[] { 42, 40, 45 };
        yield return new object[] { 10, 5, 15 };
    }

    /// <summary>
    /// Collection test data
    /// </summary>
    public static IEnumerable<int> NumberCollection => new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    /// <summary>
    /// String collection test data
    /// </summary>
    public static IEnumerable<string> StringCollection => new[] { "apple", "banana", "cherry", "date", "elderberry" };

    /// <summary>
    /// Async delay duration for async tests (milliseconds)
    /// </summary>
    public const int AsyncDelayMs = 1;
}
