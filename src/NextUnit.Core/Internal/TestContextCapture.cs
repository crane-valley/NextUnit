using System.Collections.ObjectModel;
using NextUnit.Core;

namespace NextUnit.Internal;

/// <summary>
/// Implementation of <see cref="ITestContext"/> that captures test context information for a single test case.
/// </summary>
internal sealed class TestContextCapture : ITestContext
{
    private readonly Dictionary<string, object?> _stateBag = new();

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
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public ITestOutput Output { get; }

    /// <inheritdoc/>
    public IDictionary<string, object?> StateBag => _stateBag;
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

    private static readonly IReadOnlyList<string> EmptyStringList = Array.Empty<string>();
    private static readonly IDictionary<string, object?> EmptyStateBag =
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
    public IReadOnlyList<string> Categories => EmptyStringList;

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => EmptyStringList;

    /// <inheritdoc/>
    public object?[]? Arguments => null;

    /// <inheritdoc/>
    public int? TimeoutMs => null;

    /// <inheritdoc/>
    public CancellationToken CancellationToken => CancellationToken.None;

    /// <inheritdoc/>
    public ITestOutput Output => NullTestOutput.Instance;

    /// <inheritdoc/>
    public IDictionary<string, object?> StateBag => EmptyStateBag;
}
