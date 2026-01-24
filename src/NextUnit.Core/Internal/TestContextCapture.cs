using System.Collections.ObjectModel;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Implementation of <see cref="ITestContext"/> that captures test context information for a single test case.
/// </summary>
internal sealed class TestContextCapture : ITestContext
{
    private readonly Dictionary<string, object?> _stateBag = new();
    private readonly List<Artifact> _artifacts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContextCapture"/> class.
    /// </summary>
    /// <param name="testName">The name of the test method.</param>
    /// <param name="className">The name of the test class.</param>
    /// <param name="assemblyName">The name of the assembly containing the test.</param>
    /// <param name="fullyQualifiedName">The fully qualified name of the test.</param>
    /// <param name="categories">The categories assigned to the test.</param>
    /// <param name="tags">The tags assigned to the test.</param>
    /// <param name="arguments">The arguments for parameterized tests, or null.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, or null.</param>
    /// <param name="repeatIndex">The zero-based repeat index for repeated tests, or null.</param>
    /// <param name="cancellationToken">The cancellation token for the test.</param>
    /// <param name="output">The test output writer.</param>
    public TestContextCapture(
        string testName,
        string className,
        string assemblyName,
        string fullyQualifiedName,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags,
        object?[]? arguments,
        int? timeoutMs,
        int? repeatIndex,
        CancellationToken cancellationToken,
        ITestOutput output)
    {
        TestName = testName;
        ClassName = className;
        AssemblyName = assemblyName;
        FullyQualifiedName = fullyQualifiedName;
        Categories = categories;
        Tags = tags;
        Arguments = arguments;
        TimeoutMs = timeoutMs;
        RepeatIndex = repeatIndex;
        CancellationToken = cancellationToken;
        Output = output;
    }

    /// <inheritdoc/>
    public string TestName { get; }

    /// <inheritdoc/>
    public string ClassName { get; }

    /// <inheritdoc/>
    public string AssemblyName { get; }

    /// <inheritdoc/>
    public string FullyQualifiedName { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Categories { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags { get; }

    /// <inheritdoc/>
    public object?[]? Arguments { get; }

    /// <inheritdoc/>
    public int? TimeoutMs { get; }

    /// <inheritdoc/>
    public int? RepeatIndex { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public ITestOutput Output { get; }

    /// <inheritdoc/>
    public IDictionary<string, object?> StateBag => _stateBag;

    /// <inheritdoc/>
    public IReadOnlyList<Artifact> Artifacts => _artifacts;

    /// <inheritdoc/>
    public void AttachArtifact(string filePath, string? description = null)
    {
        AttachArtifact(new Artifact { FilePath = filePath, Description = description });
    }

    /// <inheritdoc/>
    public void AttachArtifact(Artifact artifact)
    {
        if (!File.Exists(artifact.FilePath))
        {
            throw new FileNotFoundException("Artifact file not found", artifact.FilePath);
        }

        _artifacts.Add(new Artifact
        {
            FilePath = Path.GetFullPath(artifact.FilePath),
            Description = artifact.Description,
            MimeType = artifact.MimeType ?? GetMimeType(artifact.FilePath)
        });
    }

    private static readonly Dictionary<string, string> _mimeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt", "text/plain" },
        { ".log", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".mp4", "video/mp4" },
        { ".webm", "video/webm" },
        { ".pdf", "application/pdf" },
        { ".zip", "application/zip" },
    };

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return _mimeTypeMappings.TryGetValue(ext, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }
}

/// <summary>
/// No-op implementation of <see cref="ITestContext"/> used for class-level and assembly-level lifecycle instances.
/// Returns default/empty values for all properties.
/// </summary>
internal sealed class NullTestContext : ITestContext
{
    /// <summary>
    /// Singleton instance of the null test context.
    /// </summary>
    public static readonly NullTestContext Instance = new();

    private static readonly IReadOnlyList<string> _emptyStringList = Array.Empty<string>();
    private static readonly IReadOnlyList<Artifact> _emptyArtifactList = Array.Empty<Artifact>();
    private static readonly IDictionary<string, object?> _emptyStateBag =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private NullTestContext() { }

    /// <inheritdoc/>
    public string TestName => "";

    /// <inheritdoc/>
    public string ClassName => "";

    /// <inheritdoc/>
    public string AssemblyName => "";

    /// <inheritdoc/>
    public string FullyQualifiedName => "";

    /// <inheritdoc/>
    public IReadOnlyList<string> Categories => _emptyStringList;

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => _emptyStringList;

    /// <inheritdoc/>
    public object?[]? Arguments => null;

    /// <inheritdoc/>
    public int? TimeoutMs => null;

    /// <inheritdoc/>
    public int? RepeatIndex => null;

    /// <inheritdoc/>
    public CancellationToken CancellationToken => CancellationToken.None;

    /// <inheritdoc/>
    public ITestOutput Output => NullTestOutput.Instance;

    /// <inheritdoc/>
    public IDictionary<string, object?> StateBag => _emptyStateBag;

    /// <inheritdoc/>
    public IReadOnlyList<Artifact> Artifacts => _emptyArtifactList;

    /// <inheritdoc/>
    public void AttachArtifact(string filePath, string? description = null)
    {
        // No-op for null context
    }

    /// <inheritdoc/>
    public void AttachArtifact(Artifact artifact)
    {
        // No-op for null context
    }
}
