namespace NextUnit.Core.Tests;

/// <summary>
/// Behavioral tests for the collection assertions: Contains, DoesNotContain, All,
/// Single, Empty, NotEmpty, Equivalent, Subset, and Disjoint.
/// </summary>
public class AssertCollectionTests
{
    [Test]
    public void Contains_ElementPresent_DoesNotThrow()
    {
        Assert.Contains(2, new[] { 1, 2, 3 });
    }

    [Test]
    public void Contains_ElementAbsent_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Contains(9, new[] { 1, 2, 3 }));
        Assert.Contains("does not contain expected element", ex.Message);
    }

    [Test]
    public void Contains_NullCollection_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Assert.Contains(1, (IEnumerable<int>)null!));
    }

    [Test]
    public void ContainsPredicate_Match_ReturnsFirstMatch()
    {
        var result = Assert.Contains(new[] { 1, 2, 3, 4 }, x => x > 2);
        Assert.Equal(3, result);
    }

    [Test]
    public void ContainsPredicate_NoMatch_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Contains(new[] { 1, 2 }, x => x > 5));
        Assert.Contains("matching the predicate", ex.Message);
    }

    [Test]
    public void DoesNotContain_ElementAbsent_DoesNotThrow()
    {
        Assert.DoesNotContain(9, new[] { 1, 2, 3 });
    }

    [Test]
    public void DoesNotContain_ElementPresent_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotContain(2, new[] { 1, 2, 3 }));
        Assert.Contains("should not contain element", ex.Message);
    }

    [Test]
    public void DoesNotContainPredicate_NoMatch_DoesNotThrow()
    {
        Assert.DoesNotContain(new[] { 1, 2, 3 }, x => x > 5);
    }

    [Test]
    public void DoesNotContainPredicate_Match_Throws()
    {
        Assert.Throws<AssertionFailedException>(
            () => Assert.DoesNotContain(new[] { 1, 2, 6 }, x => x > 5));
    }

    [Test]
    public void All_AllPass_DoesNotThrow()
    {
        Assert.All(new[] { 2, 4, 6 }, x => Assert.True(x % 2 == 0));
    }

    [Test]
    public void All_OneFails_ThrowsWithFailingIndex()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.All(new[] { 2, 3, 6 }, x => Assert.True(x % 2 == 0)));
        Assert.Contains("Assert.All failed at index 1", ex.Message);
    }

    [Test]
    public void All_NullAction_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Assert.All(new[] { 1 }, (Action<int>)null!));
    }

    [Test]
    public void Single_OneElement_ReturnsElement()
    {
        Assert.Equal(42, Assert.Single(new[] { 42 }));
    }

    [Test]
    public void Single_Empty_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Single(Array.Empty<int>()));
        Assert.Contains("empty", ex.Message);
    }

    [Test]
    public void Single_Multiple_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Single(new[] { 1, 2 }));
        Assert.Contains("2 elements", ex.Message);
    }

    [Test]
    public void SinglePredicate_OneMatch_ReturnsElement()
    {
        Assert.Equal(3, Assert.Single(new[] { 1, 2, 3 }, x => x == 3));
    }

    [Test]
    public void SinglePredicate_NoMatch_Throws()
    {
        Assert.Throws<AssertionFailedException>(() => Assert.Single(new[] { 1, 2 }, x => x == 9));
    }

    [Test]
    public void SinglePredicate_MultipleMatches_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Single(new[] { 4, 5, 6 }, x => x > 4));
        Assert.Contains("multiple elements", ex.Message);
    }

    [Test]
    public void Empty_EmptyCollection_DoesNotThrow()
    {
        Assert.Empty(Array.Empty<int>());
    }

    [Test]
    public void Empty_NonEmptyCollection_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.Empty(new[] { 1 }));
        Assert.Contains("not empty", ex.Message);
    }

    [Test]
    public void NotEmpty_NonEmptyCollection_DoesNotThrow()
    {
        Assert.NotEmpty(new[] { 1 });
    }

    [Test]
    public void NotEmpty_EmptyCollection_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(() => Assert.NotEmpty(Array.Empty<int>()));
        Assert.Contains("empty", ex.Message);
    }

    [Test]
    public void Equivalent_SameElementsDifferentOrder_DoesNotThrow()
    {
        Assert.Equivalent(new[] { 1, 2, 3 }, new[] { 3, 1, 2 });
    }

    [Test]
    public void Equivalent_DifferentCounts_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Equivalent(new[] { 1, 2 }, new[] { 1, 2, 3 }));
        Assert.Contains("different counts", ex.Message);
    }

    [Test]
    public void Equivalent_DifferentElements_Throws()
    {
        Assert.Throws<AssertionFailedException>(
            () => Assert.Equivalent(new[] { 1, 2, 3 }, new[] { 1, 2, 4 }));
    }

    [Test]
    public void Equivalent_DifferentDuplicateCounts_Throws()
    {
        Assert.Throws<AssertionFailedException>(
            () => Assert.Equivalent(new[] { 1, 1, 2 }, new[] { 1, 2, 2 }));
    }

    [Test]
    public void Subset_AllPresent_DoesNotThrow()
    {
        Assert.Subset(new[] { 1, 2 }, new[] { 1, 2, 3 });
    }

    [Test]
    public void Subset_MissingElement_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Subset(new[] { 1, 9 }, new[] { 1, 2, 3 }));
        Assert.Contains("not found in superset", ex.Message);
    }

    [Test]
    public void Disjoint_NoCommonElements_DoesNotThrow()
    {
        Assert.Disjoint(new[] { 1, 2 }, new[] { 3, 4 });
    }

    [Test]
    public void Disjoint_CommonElement_Throws()
    {
        var ex = Assert.Throws<AssertionFailedException>(
            () => Assert.Disjoint(new[] { 1, 2 }, new[] { 2, 3 }));
        Assert.Contains("common element", ex.Message);
    }
}
