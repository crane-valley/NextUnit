namespace NextUnit;

/// <summary>
/// Provides data and optional metadata for one test case produced by a data source.
/// </summary>
public interface ITestDataRow
{
    /// <summary>
    /// Gets the untyped data value supplied to the test method.
    /// </summary>
    public object? Data { get; }

    /// <summary>
    /// Gets the display name override for this test case.
    /// </summary>
    public string? DisplayName { get; }

    /// <summary>
    /// Gets the categories added to this test case.
    /// </summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>
    /// Gets the tags added to this test case.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the reason this test case should be skipped.
    /// </summary>
    public string? SkipReason { get; }
}

/// <summary>
/// Provides strongly typed data and optional metadata for one test case.
/// </summary>
/// <typeparam name="T">The data type. Tuples are expanded across test method parameters.</typeparam>
public sealed class TestDataRow<T> : ITestDataRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataRow{T}"/> class.
    /// </summary>
    /// <param name="data">The data supplied to the test method.</param>
    /// <param name="displayName">An optional display name override.</param>
    /// <param name="categories">Optional categories added to the test case.</param>
    /// <param name="tags">Optional tags added to the test case.</param>
    /// <param name="skipReason">An optional reason to skip this test case.</param>
    public TestDataRow(
        T data,
        string? displayName = null,
        IEnumerable<string>? categories = null,
        IEnumerable<string>? tags = null,
        string? skipReason = null)
    {
        Data = data;
        DisplayName = ValidateOptionalText(displayName, nameof(displayName));
        Categories = ValidateLabels(categories, nameof(categories));
        Tags = ValidateLabels(tags, nameof(tags));
        SkipReason = ValidateOptionalText(skipReason, nameof(skipReason));
    }

    /// <summary>
    /// Gets the strongly typed data supplied to the test method.
    /// </summary>
    public T Data { get; }

    /// <inheritdoc />
    public string? DisplayName { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Categories { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Tags { get; }

    /// <inheritdoc />
    public string? SkipReason { get; }

    object? ITestDataRow.Data => Data;

    private static string? ValidateOptionalText(string? value, string parameterName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
        }

        return value;
    }

    private static IReadOnlyList<string> ValidateLabels(
        IEnumerable<string>? values,
        string parameterName)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Labels cannot contain null, empty, or whitespace values.",
                    parameterName);
            }

            if (!result.Contains(value, StringComparer.Ordinal))
            {
                result.Add(value);
            }
        }

        return result.ToArray();
    }
}
