namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating and validating category/tag filtering functionality.
/// These tests validate that filtering works correctly.
/// </summary>
[Category("FilteringTests")]
public class FilterValidationTests
{
    [Test]
    [Category("Validation")]
    [Tag("Unit")]
    public void CategoryFiltering_IncludesMatchingTests()
    {
        // This test validates that category filtering is working
        // When NEXTUNIT_INCLUDE_CATEGORIES=Validation is set, this test should run
        Assert.True(true);
    }

    [Test]
    [Category("Validation")]
    [Tag("Integration")]
    public void CategoryFiltering_ExcludesNonMatchingTests()
    {
        // This test also has Validation category
        Assert.True(true);
    }

    [Test]
    [Tag("Unit")]
    public void TagFiltering_IncludesMatchingTests()
    {
        // This test has Unit tag and inherits FilteringTests category from class
        Assert.True(true);
    }

    [Test]
    [Category("Special")]
    [Tag("Performance")]
    public void MultipleAttributes_WorkCorrectly()
    {
        // This test has both Special category and Performance tag
        // Plus inherits FilteringTests from class
        Assert.True(true);
    }

    [Test]
    public void InheritedCategoryOnly_WorksCorrectly()
    {
        // This test only has the inherited FilteringTests category from class
        Assert.True(true);
    }
}
