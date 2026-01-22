using System.Text.RegularExpressions;

namespace NextUnit.Platform;

/// <summary>
/// Contains filter configuration for test execution.
/// </summary>
internal sealed class TestFilterConfiguration
{
    private IReadOnlyList<string> _testNamePatterns = Array.Empty<string>();
    private IReadOnlyList<Regex> _compiledWildcardPatterns = Array.Empty<Regex>();

    /// <summary>
    /// Gets or sets the categories to include. Only tests with at least one of these categories will run.
    /// </summary>
    public IReadOnlyList<string> IncludeCategories { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the categories to exclude. Tests with any of these categories will not run.
    /// </summary>
    public IReadOnlyList<string> ExcludeCategories { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the tags to include. Only tests with at least one of these tags will run.
    /// </summary>
    public IReadOnlyList<string> IncludeTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the tags to exclude. Tests with any of these tags will not run.
    /// </summary>
    public IReadOnlyList<string> ExcludeTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the test name patterns to include (supports * and ? wildcards).
    /// Patterns are compiled to regex on assignment for better performance.
    /// </summary>
    public IReadOnlyList<string> TestNamePatterns
    {
        get => _testNamePatterns;
        set
        {
            _testNamePatterns = value;
            _compiledWildcardPatterns = CompileWildcardPatterns(value);
        }
    }

    /// <summary>
    /// Gets or sets the test name regular expression patterns to include.
    /// </summary>
    public IReadOnlyList<Regex> TestNameRegexPatterns { get; set; } = Array.Empty<Regex>();

    /// <summary>
    /// Determines whether a test should be included based on the filter configuration.
    /// </summary>
    /// <param name="categories">The categories assigned to the test.</param>
    /// <param name="tags">The tags assigned to the test.</param>
    /// <param name="testName">The full name of the test.</param>
    /// <returns><c>true</c> if the test should be included; otherwise, <c>false</c>.</returns>
    public bool ShouldIncludeTest(IReadOnlyList<string> categories, IReadOnlyList<string> tags, string testName)
    {
        // Exclude filters take precedence
        if (ExcludeCategories.Count > 0 && categories.Any(c => ExcludeCategories.Contains(c, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (ExcludeTags.Count > 0 && tags.Any(t => ExcludeTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        // If no include filters are specified, test passes
        var hasIncludeFilters = IncludeCategories.Count > 0 || IncludeTags.Count > 0
            || _compiledWildcardPatterns.Count > 0 || TestNameRegexPatterns.Count > 0;
        if (!hasIncludeFilters)
        {
            return true;
        }

        // Test must match at least one include filter (OR logic)
        var matchesCategory = IncludeCategories.Count > 0 && categories.Any(c => IncludeCategories.Contains(c, StringComparer.OrdinalIgnoreCase));
        var matchesTag = IncludeTags.Count > 0 && tags.Any(t => IncludeTags.Contains(t, StringComparer.OrdinalIgnoreCase));
        var matchesNamePattern = _compiledWildcardPatterns.Count > 0 && _compiledWildcardPatterns.Any(regex => regex.IsMatch(testName));
        var matchesRegex = TestNameRegexPatterns.Count > 0 && TestNameRegexPatterns.Any(regex => regex.IsMatch(testName));

        return matchesCategory || matchesTag || matchesNamePattern || matchesRegex;
    }

    /// <summary>
    /// Compiles wildcard patterns (* and ?) to cached regex objects.
    /// </summary>
    private static IReadOnlyList<Regex> CompileWildcardPatterns(IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return Array.Empty<Regex>();
        }

        return patterns.Select(pattern =>
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }).ToList();
    }
}
