using Microsoft.CodeAnalysis;

namespace NextUnit.Analyzers;

/// <summary>
/// Diagnostic descriptors for all NextUnit analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "NextUnit";

    /// <summary>
    /// NU0001: Test or lifecycle method should not be async void.
    /// </summary>
    public static readonly DiagnosticDescriptor AsyncVoidTest = new(
        id: "NU0001",
        title: "Test or lifecycle method should not be async void",
        messageFormat: "Method '{0}' is async void; change return type to Task",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Async test and lifecycle methods should return Task instead of void so NextUnit can await completion and propagate exceptions.");

    /// <summary>
    /// NU0002: Test method must be public.
    /// </summary>
    public static readonly DiagnosticDescriptor TestMethodNotPublic = new(
        id: "NU0002",
        title: "Test method must be public",
        messageFormat: "Test method '{0}' must be public",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Test methods must be declared as public to be discovered and executed by the test runner.");

    /// <summary>
    /// NU0003: TestData member not found.
    /// </summary>
    public static readonly DiagnosticDescriptor TestDataMemberNotFound = new(
        id: "NU0003",
        title: "TestData member not found",
        messageFormat: "TestData member '{0}' was not found or is not accessible in type '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The member specified in [TestData] attribute must exist and be accessible.");

    /// <summary>
    /// NU0004: Arguments count mismatch.
    /// </summary>
    public static readonly DiagnosticDescriptor ArgumentsCountMismatch = new(
        id: "NU0004",
        title: "Arguments count mismatch",
        messageFormat: "Method '{0}' has {1} parameter(s) but [Arguments] provides {2} value(s)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The number of values in [Arguments] attribute must match the number of method parameters.");

    /// <summary>
    /// NU0005: Lifecycle method should handle exceptions.
    /// </summary>
    public static readonly DiagnosticDescriptor LifecycleMethodThrows = new(
        id: "NU0005",
        title: "Lifecycle method may throw",
        messageFormat: "Before/After method '{0}' may throw unhandled exceptions which could affect other tests",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Lifecycle methods (Before/After) that throw exceptions may cause subsequent tests to be skipped or fail unexpectedly.");

    /// <summary>
    /// NU0006: Invalid timeout value.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidTimeout = new(
        id: "NU0006",
        title: "Invalid timeout value",
        messageFormat: "Timeout value must be positive, got {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The timeout value in [Timeout] attribute must be a positive integer representing milliseconds.");

    /// <summary>
    /// NU0007: DependsOn target not found.
    /// </summary>
    public static readonly DiagnosticDescriptor DependsOnNotFound = new(
        id: "NU0007",
        title: "DependsOn target not found",
        messageFormat: "DependsOn references '{0}' which was not found in test class '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The test method referenced in [DependsOn] attribute must exist in the same test class.");

    /// <summary>
    /// NU0008: MatrixExclusion parameter count mismatch.
    /// </summary>
    public static readonly DiagnosticDescriptor MatrixExclusionCountMismatch = new(
        id: "NU0008",
        title: "MatrixExclusion parameter count mismatch",
        messageFormat: "MatrixExclusion has {0} value(s) but method has {1} matrix parameter(s)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The number of values in [MatrixExclusion] attribute must match the number of [Matrix] parameters.");

    /// <summary>
    /// NU0009: Data source row type is incompatible with the test method.
    /// </summary>
    public static readonly DiagnosticDescriptor TestDataRowTypeMismatch = new(
        id: "NU0009",
        title: "Test data row type does not match test method parameters",
        messageFormat: "Data source '{0}' supplies row type '{1}', which is incompatible with test method '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Statically typed test data rows must provide values that can be passed to the test method parameters.");

    /// <summary>
    /// NU0010: Test data row metadata is invalid.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidTestDataRowMetadata = new(
        id: "NU0010",
        title: "Test data row metadata is invalid",
        messageFormat: "Test data row metadata '{0}' cannot contain an empty or whitespace value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Constant test data row display names, skip reasons, categories, and tags must contain meaningful text.");

    /// <summary>
    /// NU0011: Test or lifecycle method return type is unsupported.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedMethodReturnType = new(
        id: "NU0011",
        title: "Test or lifecycle method return type is unsupported",
        messageFormat: "Method '{0}' has unsupported return type '{1}'; use void, Task, Task<T>, ValueTask, or ValueTask<T>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Test and lifecycle methods must return void, Task, Task<T>, ValueTask, or ValueTask<T> so NextUnit can observe completion and failures.");

    // NU0012 is intentionally unused -- reserved to avoid ID churn with other in-flight work.

    /// <summary>
    /// NU0013: Data source attribute requires [Test].
    /// </summary>
    public static readonly DiagnosticDescriptor DataSourceWithoutTest = new(
        id: "NU0013",
        title: "Data source attribute without [Test]",
        messageFormat: "Method '{0}' has a data-source attribute ([{1}]) but is missing [Test]; the generator will silently ignore it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods with [Arguments], [TestData], or [Matrix] must also have [Test] to be discovered by the test generator.");

    /// <summary>
    /// NU0014: TestData member returns an awaitable that cannot supply rows.
    /// </summary>
    public static readonly DiagnosticDescriptor TestDataMemberUnsupportedAwaitable = new(
        id: "NU0014",
        title: "TestData member returns an unsupported awaitable",
        messageFormat: "TestData member '{0}' returns '{1}', which supplies no rows; use IEnumerable<T>, IAsyncEnumerable<T>, Task<IEnumerable<T>>, or ValueTask<IEnumerable<T>>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [TestData] member may await, but the awaited value must be a collection of rows. Bare Task or ValueTask, and a task wrapping a non-collection, cannot be expanded into test cases.");

    /// <summary>
    /// NU0015: Conflicting [Retry] and [Retry&lt;TPolicy&gt;] attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor ConflictingRetryAttributes = new(
        id: "NU0015",
        title: "Conflicting retry attributes",
        messageFormat: "'{0}' has more than one retry attribute; keep one, because only one attempt budget and policy can apply",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[Retry] and every constructed [Retry<TPolicy>] declare the attempt budget and delay for the same target, so applying more than one leaves the retry policy and the budget ambiguous.");

    /// <summary>
    /// NU0016: Retry policy type is not reachable from the generated registry.
    /// </summary>
    public static readonly DiagnosticDescriptor RetryPolicyNotAccessible = new(
        id: "NU0016",
        title: "Retry policy type is not accessible to generated code",
        messageFormat: "Retry policy '{0}' is not accessible from the generated test registry; make it internal or public and not nested in a private or protected scope",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator constructs the retry policy directly rather than by reflection, so the policy type must be visible from the generated registry. A private or protected policy satisfies the new() constraint at the attribute but fails to compile in the generated code.");

    /// <summary>
    /// NU0017: Invalid retry count.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidRetryCount = new(
        id: "NU0017",
        title: "Invalid retry count",
        messageFormat: "Retry count must be at least 1, got {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The retry count is the total number of attempts, including the first, so it must be at least 1. The attribute constructor rejects a smaller value, but the generated execution path reads the attribute arguments without ever constructing the attribute, so the guard never runs.");

    /// <summary>
    /// NU0018: Malformed culture name.
    /// </summary>
    public static readonly DiagnosticDescriptor MalformedCultureName = new(
        id: "NU0018",
        title: "Malformed culture name",
        messageFormat: "Culture name '{0}' is not well formed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A culture name may contain only letters, digits, '-' and '_', and may not begin or end with a separator or run two together. Whether a well-formed name matches an installed culture depends on the machine running the test, so only names that no machine could accept are reported at build time; the rest are reported against the test that declared them when it runs.");

    /// <summary>
    /// NU0019: Invalid parallel limit.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidParallelLimit = new(
        id: "NU0019",
        title: "Invalid parallel limit",
        messageFormat: "Parallel limit must be positive, got {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The value in [ParallelLimit] becomes ParallelOptions.MaxDegreeOfParallelism, whose setter throws for 0 and for anything below -1, and that throw aborts the whole run rather than failing the test that declared it. The one remaining non-positive value, -1, is accepted by the setter but means the processor count to Parallel.ForEachAsync rather than no limit, which is what the attribute already means when it is absent; it is rejected too so that a declared limit is always a limit.");

    /// <summary>
    /// NU0020: Data source member is not reachable from the generated registry.
    /// </summary>
    public static readonly DiagnosticDescriptor DataSourceMemberNotAccessible = new(
        id: "NU0020",
        title: "Data source member is not accessible to generated code",
        messageFormat: "Data source member '{0}' on type '{1}' is not accessible from the generated test registry; make it public or internal, and not nested in a private or protected scope",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator emits direct member access rather than reflecting, which is what keeps data sources AOT-safe, so the member has to be visible from the generated registry. Internal is enough: the registry is emitted into the test assembly itself. A private, protected, or private protected member, or one declared in a type that is not visible there, compiles as written and then fails the build with CS0122 inside generated code.");

    /// <summary>
    /// NU0021: Cancellation-aware data source member returns a synchronous collection.
    /// </summary>
    public static readonly DiagnosticDescriptor DataSourceCancellationTokenOnSyncSource = new(
        id: "NU0021",
        title: "Cancellation-aware data source member returns a synchronous collection",
        messageFormat: "TestData member '{0}' takes a CancellationToken but returns '{1}', which implements IEnumerable<T> as well as IAsyncEnumerable<T> and is therefore read synchronously; drop the parameter, or return a type that implements only IAsyncEnumerable<T>",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A data source type implementing both element interfaces is classified as synchronous, so that a type which meant IEnumerable<T> before asynchronous sources existed keeps meaning it. The synchronous provider takes no arguments, so there is no token to pass and the member binds to nothing; without this rule the only symptom is a parameter-count failure from the runtime reflection fallback, which mentions neither the token nor the reason.");
}
