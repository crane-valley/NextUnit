namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating enhanced assertion functionality (Priority 1.1 features).
/// </summary>
public class EnhancedAssertionTests
{
    // Approximate Equality Assertions for Floating-Point

    [Test]
    public void Equal_WithPrecision_ComparesDoubles()
    {
        var expected = 3.14159;
        var actual = 3.14158;
        Assert.Equal(expected, actual, precision: 4);
    }

    [Test]
    public void Equal_WithPrecision_ComparesDecimals()
    {
        var expected = 100.123456m;
        var actual = 100.123455m;
        Assert.Equal(expected, actual, precision: 5);
    }

    [Test]
    public void Equal_WithPrecision_HandlesNaN()
    {
        var expected = double.NaN;
        var actual = double.NaN;
        Assert.Equal(expected, actual, precision: 2);
    }

    [Test]
    public void Equal_WithPrecision_HandlesInfinity()
    {
        var expected = double.PositiveInfinity;
        var actual = double.PositiveInfinity;
        Assert.Equal(expected, actual, precision: 2);
    }

    [Test]
    public void NotEqual_WithPrecision_ComparesDoubles()
    {
        var notExpected = 3.14;
        var actual = 2.71;
        Assert.NotEqual(notExpected, actual, precision: 2);
    }

    [Test]
    public void NotEqual_WithPrecision_ComparesDecimals()
    {
        var notExpected = 100.0m;
        var actual = 200.0m;
        Assert.NotEqual(notExpected, actual, precision: 0);
    }

    [Test]
    [Arguments(1.23456, 1.23455, 4)]
    [Arguments(100.0, 100.005, 2)]
    [Arguments(0.0001, 0.00015, 3)]
    public void Equal_WithPrecision_ParameterizedTests(double expected, double actual, int precision)
    {
        Assert.Equal(expected, actual, precision);
    }

    [Test]
    public void Equal_WithNegativePrecision_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0, 1.0, precision: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.Equal(1.0m, 1.0m, precision: -1));
    }

    [Test]
    public void NotEqual_WithNegativePrecision_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0, 2.0, precision: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Assert.NotEqual(1.0m, 2.0m, precision: -1));
    }

    // Custom Comparer Support

    [Test]
    public void Equal_WithCustomComparer_UsesComparer()
    {
        var expected = "hello";
        var actual = "HELLO";
        Assert.Equal(expected, actual, StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public void Equal_WithCustomComparer_ComparesCollections()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new[] { 1, 2, 3 };
        Assert.Equal(expected, actual, new ArrayEqualityComparer<int>());
    }

    private class ArrayEqualityComparer<T> : IEqualityComparer<T[]>
    {
        public bool Equals(T[]? x, T[]? y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.SequenceEqual(y);
        }

        public int GetHashCode(T[] obj)
        {
            return obj.GetHashCode();
        }
    }

    // Enhanced Exception Assertions with Message Matching

    [Test]
    public void Throws_WithExpectedMessage_MatchesExactMessage()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => throw new ArgumentException("Invalid argument"),
            expectedMessage: "Invalid argument");

        Assert.Equal("Invalid argument", exception.Message);
    }

    [Test]
    public async Task ThrowsAsync_WithExpectedMessage_MatchesExactMessageAsync()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("Operation failed");
            },
            expectedMessage: "Operation failed");

        Assert.Equal("Operation failed", exception.Message);
    }

    [Test]
    [Arguments("First error")]
    [Arguments("Second error")]
    public void Throws_WithExpectedMessage_ParameterizedTests(string expectedMessage)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => throw new ArgumentException(expectedMessage),
            expectedMessage: expectedMessage);

        Assert.Equal(expectedMessage, exception.Message);
    }

    // Collection Comparison Assertions

    [Test]
    public void Equivalent_VerifiesUnorderedEquality()
    {
        var expected = new[] { 1, 2, 3, 4, 5 };
        var actual = new[] { 5, 3, 1, 4, 2 };
        Assert.Equivalent(expected, actual);
    }

    [Test]
    public void Equivalent_HandlesMultipleOccurrences()
    {
        var expected = new[] { 1, 2, 2, 3, 3, 3 };
        var actual = new[] { 3, 1, 3, 2, 3, 2 };
        Assert.Equivalent(expected, actual);
    }

    [Test]
    public void Equivalent_WorksWithStrings()
    {
        var expected = new[] { "apple", "banana", "cherry" };
        var actual = new[] { "cherry", "apple", "banana" };
        Assert.Equivalent(expected, actual);
    }

    [Test]
    public void Subset_VerifiesSubsetRelationship()
    {
        var subset = new[] { 2, 4 };
        var superset = new[] { 1, 2, 3, 4, 5 };
        Assert.Subset(subset, superset);
    }

    [Test]
    public void Subset_WorksWithEmptySubset()
    {
        var subset = Array.Empty<int>();
        var superset = new[] { 1, 2, 3 };
        Assert.Subset(subset, superset);
    }

    [Test]
    public void Subset_WorksWithComplexTypes()
    {
        var subset = new[] { "apple", "cherry" };
        var superset = new[] { "apple", "banana", "cherry", "date" };
        Assert.Subset(subset, superset);
    }

    [Test]
    public void Disjoint_VerifiesNoCommonElements()
    {
        var collection1 = new[] { 1, 2, 3 };
        var collection2 = new[] { 4, 5, 6 };
        Assert.Disjoint(collection1, collection2);
    }

    [Test]
    public void Disjoint_WorksWithEmptyCollections()
    {
        var collection1 = Array.Empty<int>();
        var collection2 = new[] { 1, 2, 3 };
        Assert.Disjoint(collection1, collection2);
    }

    [Test]
    public void Disjoint_WorksWithStrings()
    {
        var collection1 = new[] { "apple", "banana" };
        var collection2 = new[] { "cherry", "date" };
        Assert.Disjoint(collection1, collection2);
    }

    // Combined Scenarios

    [Test]
    public void ScientificCalculations_UsePrecisionAssertions()
    {
        var pi = Math.PI;
        var approximation = 3.14159;
        Assert.Equal(pi, approximation, precision: 5);
    }

    [Test]
    public void FinancialCalculations_UseDecimalPrecision()
    {
        var expected = 100.00m;
        var actual = 99.999m;
        Assert.Equal(expected, actual, precision: 2);
    }

    [Test]
    public void SetOperations_VerifyDisjointSets()
    {
        var evenNumbers = new[] { 2, 4, 6, 8 };
        var oddNumbers = new[] { 1, 3, 5, 7 };
        Assert.Disjoint(evenNumbers, oddNumbers);
    }

    [Test]
    public void SetOperations_VerifySubset()
    {
        var primeNumbers = new[] { 2, 3, 5, 7 };
        var allPrimes = new[] { 2, 3, 5, 7, 11, 13 };
        Assert.Subset(primeNumbers, allPrimes);
    }

    [Test]
    public void CollectionTransformation_PreservesElements()
    {
        var original = new[] { 1, 2, 3, 4, 5 };
        var shuffled = original.OrderByDescending(x => x).ToArray();
        Assert.Equivalent(original, shuffled);
    }

    [Test]
    public void ExceptionHandling_ValidatesErrorMessages()
    {
        var parameter = "userId";

        var exception = Assert.Throws<ArgumentNullException>(
            () => ArgumentNullException.ThrowIfNull((object?)null, parameter));

        // Check that the exception message contains the parameter name for robustness across .NET versions/cultures
        Assert.Contains(parameter, exception.Message);
        Assert.Equal(parameter, exception.ParamName);
    }
}
