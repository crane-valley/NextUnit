namespace NextUnit.Platform;

/// <summary>
/// Contains filter configuration for test execution.
/// </summary>
internal sealed class TestFilterConfiguration
{
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
    /// Determines whether a test should be included based on the filter configuration.
    /// </summary>
    /// <param name="categories">The categories assigned to the test.</param>
    /// <param name="tags">The tags assigned to the test.</param>
    /// <returns><c>true</c> if the test should be included; otherwise, <c>false</c>.</returns>
    public bool ShouldIncludeTest(IReadOnlyList<string> categories, IReadOnlyList<string> tags)
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

        // If include filters are specified, test must match at least one
        var hasIncludeFilters = IncludeCategories.Count > 0 || IncludeTags.Count > 0;
        if (!hasIncludeFilters)
        {
            return true; // No include filters, test passes
        }

        var matchesCategory = IncludeCategories.Count == 0 || categories.Any(c => IncludeCategories.Contains(c, StringComparer.OrdinalIgnoreCase));
        var matchesTag = IncludeTags.Count == 0 || tags.Any(t => IncludeTags.Contains(t, StringComparer.OrdinalIgnoreCase));

        return matchesCategory && matchesTag;
    }
}
