namespace NextUnit;

/// <summary>
/// Represents a file artifact attached to a test result.
/// Artifacts are files such as screenshots, logs, or videos that provide additional context for test results.
/// </summary>
public sealed class Artifact
{
    /// <summary>
    /// Gets the absolute path to the artifact file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets an optional description of the artifact.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the MIME type of the artifact.
    /// If not specified, it will be inferred from the file extension.
    /// </summary>
    public string? MimeType { get; init; }
}
