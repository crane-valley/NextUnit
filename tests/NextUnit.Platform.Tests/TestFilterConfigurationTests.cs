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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

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
        var result = config.ShouldIncludeTest(categories, tags, "TestName");

        // Assert - Should pass: matches include category, matches include tag, doesn't match any exclude
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNamePattern_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "MyTest" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "MyTest");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNamePattern_WildcardMatch_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "My*Test" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "MyAwesomeTest");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNamePattern_QuestionMarkWildcard_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "Test?" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "Test1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNamePattern_NoMatch_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "Integration*" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "UnitTest");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNameRegex_SimpleMatch_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNameRegexPatterns = new[] { new System.Text.RegularExpressions.Regex("^Test.*") }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "TestMethod");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNameRegex_ComplexPattern_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNameRegexPatterns = new[] { new System.Text.RegularExpressions.Regex(@"Test\d+_\w+") }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "Test123_ShouldPass");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNameRegex_NoMatch_ReturnsFalse()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNameRegexPatterns = new[] { new System.Text.RegularExpressions.Regex("^Integration") }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "UnitTest");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIncludeTest_MultipleTestNamePatterns_MatchesOne_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "Integration*", "Unit*" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "UnitTest");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_TestNamePattern_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            TestNamePatterns = new[] { "mytest" }
        };
        var categories = Array.Empty<string>();
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "MyTest");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIncludeTest_CombinedFilters_TestNameAndCategory_BothRequired_ReturnsTrue()
    {
        // Arrange
        var config = new TestFilterConfiguration
        {
            IncludeCategories = new[] { "Integration" },
            TestNamePatterns = new[] { "*Database*" }
        };
        var categories = new[] { "Integration" };
        var tags = Array.Empty<string>();

        // Act
        var result = config.ShouldIncludeTest(categories, tags, "Test_Database_Connection");

        // Assert
        Assert.True(result);
    }
}
