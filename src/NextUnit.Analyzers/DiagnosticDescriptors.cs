using Microsoft.CodeAnalysis;

namespace NextUnit.Analyzers;

/// <summary>
/// Diagnostic descriptors for all NextUnit analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "NextUnit";

    /// <summary>
    /// NU0001: Test method should not be async void.
    /// </summary>
    public static readonly DiagnosticDescriptor AsyncVoidTest = new(
        id: "NU0001",
        title: "Test method should not be async void",
        messageFormat: "Test method '{0}' is async void; change return type to Task",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Async test methods should return Task instead of void to properly await asynchronous operations and propagate exceptions.");

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
}
