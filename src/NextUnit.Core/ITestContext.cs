namespace NextUnit.Core;

/// <summary>
/// Provides runtime test context information during test execution.
/// This interface can be injected into test class constructors or accessed via <see cref="TestContext.Current"/>.
/// </summary>
public interface ITestContext
{
    /// <summary>
    /// Gets the name of the test method being executed.
    /// </summary>
    string TestName { get; }

    /// <summary>
    /// Gets the name of the test class containing the test method.
    /// </summary>
    string ClassName { get; }

    /// <summary>
    /// Gets the name of the assembly containing the test.
    /// </summary>
    string AssemblyName { get; }

    /// <summary>
    /// Gets the fully qualified name of the test (e.g., "Namespace.ClassName.MethodName").
    /// </summary>
    string FullyQualifiedName { get; }

    /// <summary>
    /// Gets the categories assigned to the test via [Category] attributes.
    /// </summary>
    IReadOnlyList<string> Categories { get; }

    /// <summary>
    /// Gets the tags assigned to the test via [Tag] attributes.
    /// </summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the arguments passed to the test method for parameterized tests, or <c>null</c> for non-parameterized tests.
    /// </summary>
    object?[]? Arguments { get; }

    /// <summary>
    /// Gets the timeout in milliseconds configured for this test, or <c>null</c> if no timeout is configured.
    /// </summary>
    int? TimeoutMs { get; }

    /// <summary>
    /// Gets a cancellation token that is triggered when the test should be cancelled (either due to timeout or external cancellation).
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the test output writer for this test.
    /// </summary>
    ITestOutput Output { get; }

    /// <summary>
    /// Gets a dictionary for storing test-scoped data. Data stored here is available throughout the test execution.
    /// </summary>
    IDictionary<string, object?> StateBag { get; }
}
