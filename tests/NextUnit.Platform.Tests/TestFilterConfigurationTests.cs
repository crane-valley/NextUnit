namespace NextUnit.Platform.Tests;

/// <summary>
/// Unit tests for the TestFilterConfiguration class.
/// </summary>
public class TestFilterConfigurationTests
{
    [Fact]
    public void ShouldIncludeTest_NoFilters_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration();
        var categories = new[] { "Unit", "Integration" };
        var tags = new[] { "Fast", "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_EmptyCollections_NoFilters_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration();
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeCategory_MatchingCategory_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeCategory_NonMatchingCategory_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Unit" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeCategory_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "INTEGRATION" }
        };
        var categories = new[] { "integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeTag_MatchingTag_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Fast" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeTag_NonMatchingTag_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Fast" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_IncludeTag_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "FAST" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeCategory_MatchingCategory_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeCategory_NonMatchingCategory_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Unit" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeCategory_CaseInsensitive_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeCategories = new[] { "INTEGRATION" }
        };
        var categories = new[] { "integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeTag_MatchingTag_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeTags = new[] { "Slow" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeTag_NonMatchingTag_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeTags = new[] { "Slow" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludeTag_CaseInsensitive_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeTags = new[] { "SLOW" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludePrecedence_ExcludeWins()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            ExcludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Exclude should take precedence over include
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ExcludePrecedence_TagExcludeWins()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Slow" },
            ExcludeTags = new[] { "Slow" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Exclude should take precedence over include
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_OrLogic_MatchesCategory_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            IncludeTags = new[] { "Fast" }
        };
        var categories = new[] { "Integration" };
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - OR logic: matching category should be enough
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_OrLogic_MatchesTag_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            IncludeTags = new[] { "Fast" }
        };
        var categories = new[] { "Unit" };
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - OR logic: matching tag should be enough
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_OrLogic_MatchesBoth_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            IncludeTags = new[] { "Fast" }
        };
        var categories = new[] { "Integration" };
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - OR logic: matching both should pass
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_OrLogic_MatchesNeither_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            IncludeTags = new[] { "Fast" }
        };
        var categories = new[] { "Unit" };
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - OR logic: matching neither should fail
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithNoCategories_IncludeCategoryFilter_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithNoTags_IncludeTagFilter_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Fast" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithNoCategories_ExcludeCategoryFilter_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeCategories = new[] { "Integration" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - No categories means exclude filter doesn't match
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithNoTags_ExcludeTagFilter_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeTags = new[] { "Slow" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - No tags means exclude filter doesn't match
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_MultipleIncludeCategories_MatchesAny_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration", "Unit", "E2E" }
        };
        var categories = new[] { "Unit" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_MultipleIncludeTags_MatchesAny_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Fast", "Slow", "Medium" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Medium" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_MultipleExcludeCategories_MatchesAny_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeCategories = new[] { "Integration", "E2E" }
        };
        var categories = new[] { "Integration", "Database" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Should exclude if matches any exclude category
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_MultipleExcludeTags_MatchesAny_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            ExcludeTags = new[] { "Slow", "Flaky" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Fast", "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Should exclude if matches any exclude tag
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithMultipleCategories_IncludeFilter_MatchesOne_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" }
        };
        var categories = new[] { "Unit", "Integration", "Database" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestWithMultipleTags_IncludeFilter_MatchesOne_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeTags = new[] { "Fast" }
        };
        var categories = Array.Empty<string>();
        var tags = new[] { "Slow", "Fast", "Flaky" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_ComplexScenario_IncludeAndExclude_ExcludeWins()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration", "Unit" },
            ExcludeCategories = new[] { "Flaky" },
            IncludeTags = new[] { "Fast" },
            ExcludeTags = new[] { "Slow" }
        };
        var categories = new[] { "Integration", "Flaky" };
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Exclude category should take precedence
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ComplexScenario_TagExcludeWins()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            IncludeTags = new[] { "Fast", "Slow" },
            ExcludeTags = new[] { "Slow" }
        };
        var categories = new[] { "Integration" };
        var tags = new[] { "Slow" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Exclude tag should take precedence
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_ComplexScenario_PassesAllFilters()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration", "Unit" },
            ExcludeCategories = new[] { "Flaky" },
            IncludeTags = new[] { "Fast" },
            ExcludeTags = new[] { "Slow" }
        };
        var categories = new[] { "Integration" };
        var tags = new[] { "Fast" };

        // Act
        var result = config.ShouldIncludeTest(categories, tags);

        // Assert - Should pass: matches include category, matches include tag, doesn't match any exclude
        Assert.True(result);
    }
}
