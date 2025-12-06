namespace NextUnit.SampleTests;

/// <summary>
/// Tests to verify that TestData providers are not executed when their categories/tags are excluded.
/// </summary>
[Category("TestDataFilteringValidation")]
public class TestDataFilteringTests
{
    [Test]
    [Category("ShouldBeExcluded")]
    [TestData(nameof(ThrowingDataProvider))]
    public void TestWithThrowingProvider_ShouldNotExecuteProvider(int value)
    {
        // This test should never run when ShouldBeExcluded category is excluded
        // If the data provider executes, it will throw an exception
        Assert.True(value > 0);
    }

    public static IEnumerable<object[]> ThrowingDataProvider()
    {
        // This should NOT be called when filtering excludes the "ShouldBeExcluded" category
        throw new InvalidOperationException("TestData provider should not have been executed for excluded category!");
    }

    [Test]
    [Category("ShouldBeIncluded")]
    [TestData(nameof(ValidDataProvider))]
    public void TestWithValidProvider_ShouldExecuteProvider(int value)
    {
        Assert.True(value > 0);
    }

    public static IEnumerable<object[]> ValidDataProvider()
    {
        yield return new object[] { 1 };
        yield return new object[] { 2 };
        yield return new object[] { 3 };
    }
}
