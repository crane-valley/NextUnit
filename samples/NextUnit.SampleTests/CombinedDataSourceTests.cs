using System.Collections;

namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates combined data source features using parameter-level data source attributes.
/// These tests show how to mix different data sources per parameter and compute
/// Cartesian products at runtime.
/// </summary>
public class CombinedDataSourceTests
{
    #region Basic [Values] Usage

    /// <summary>
    /// Basic test with inline values on a single parameter.
    /// </summary>
    [Test]
    public void SingleParameter_WithValues(
        [Values(1, 2, 3)] int number)
    {
        Assert.True(number >= 1 && number <= 3);
    }

    /// <summary>
    /// Test with inline values on multiple parameters - generates Cartesian product.
    /// This will generate 2 x 3 = 6 test cases.
    /// </summary>
    [Test]
    public void MultipleParameters_WithValues(
        [Values(true, false)] bool enabled,
        [Values("a", "b", "c")] string letter)
    {
        Assert.NotNull(letter);
        Assert.True(letter.Length == 1);
    }

    /// <summary>
    /// Test mixing integers and strings with Cartesian product.
    /// </summary>
    [Test]
    public void MixedTypes_WithValues(
        [Values(1, 2)] int number,
        [Values("x", "y")] string letter,
        [Values(true, false)] bool flag)
    {
        // 2 x 2 x 2 = 8 test cases
        Assert.True(number == 1 || number == 2);
        Assert.True(letter == "x" || letter == "y");
    }

    #endregion

    #region [ValuesFromMember] Usage

    public static IEnumerable<int> GetNumbers() => [10, 20, 30];

    public static IEnumerable<string> Names => ["Alice", "Bob"];

    public static readonly string[] Browsers = ["Chrome", "Firefox", "Edge"];

    /// <summary>
    /// Test using values from a static method.
    /// </summary>
    [Test]
    public void SingleParameter_ValuesFromMethod(
        [ValuesFromMember(nameof(GetNumbers))] int number)
    {
        Assert.True(number is 10 or 20 or 30);
    }

    /// <summary>
    /// Test using values from a static property.
    /// </summary>
    [Test]
    public void SingleParameter_ValuesFromProperty(
        [ValuesFromMember(nameof(Names))] string name)
    {
        Assert.True(name is "Alice" or "Bob");
    }

    /// <summary>
    /// Test using values from a static field.
    /// </summary>
    [Test]
    public void SingleParameter_ValuesFromField(
        [ValuesFromMember(nameof(Browsers))] string browser)
    {
        Assert.Contains(browser, Browsers);
    }

    /// <summary>
    /// Mixing [ValuesFromMember] with [Values] - Cartesian product.
    /// This will generate 3 x 2 = 6 test cases.
    /// </summary>
    [Test]
    public void MixedSources_MemberAndInline(
        [ValuesFromMember(nameof(GetNumbers))] int number,
        [Values(true, false)] bool enabled)
    {
        Assert.True(number >= 10);
    }

    #endregion

    #region [ValuesFrom<T>] Usage (Class Data Sources)

    /// <summary>
    /// Test using values from a class data source.
    /// </summary>
    [Test]
    public void SingleParameter_ValuesFromClass(
        [ValuesFrom<PrimeNumbers>] int prime)
    {
        // All primes from PrimeNumbers should be prime
        Assert.True(IsPrime(prime), $"{prime} should be prime");
    }

    /// <summary>
    /// Mix class data source with inline values.
    /// </summary>
    [Test]
    public void MixedSources_ClassAndInline(
        [ValuesFrom<BrowserDataSource>] string browser,
        [Values(1920, 1366)] int screenWidth)
    {
        Assert.NotNull(browser);
        Assert.True(screenWidth > 0);
    }

    #endregion

    #region Complex Scenarios

    /// <summary>
    /// Full combination: [Values], [ValuesFromMember], and [ValuesFrom&lt;T&gt;].
    /// </summary>
    [Test]
    public void AllSourceTypes_Combined(
        [Values(1, 2)] int id,
        [ValuesFromMember(nameof(Names))] string name,
        [ValuesFrom<BrowserDataSource>] string browser)
    {
        // 2 x 2 x 3 = 12 test cases
        Assert.True(id > 0);
        Assert.NotNull(name);
        Assert.NotNull(browser);
    }

    #endregion

    #region Helper Data Sources

    private static bool IsPrime(int n)
    {
        if (n < 2)
        {
            return false;
        }

        if (n == 2)
        {
            return true;
        }

        if (n % 2 == 0)
        {
            return false;
        }

        for (int i = 3; i * i <= n; i += 2)
        {
            if (n % i == 0)
            {
                return false;
            }
        }
        return true;
    }

    #endregion
}

/// <summary>
/// Data source class providing prime numbers.
/// </summary>
public sealed class PrimeNumbers : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator()
    {
        yield return 2;
        yield return 3;
        yield return 5;
        yield return 7;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Data source class providing browser names.
/// </summary>
public sealed class BrowserDataSource : IEnumerable<string>
{
    public IEnumerator<string> GetEnumerator()
    {
        yield return "Chrome";
        yield return "Firefox";
        yield return "Safari";
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
