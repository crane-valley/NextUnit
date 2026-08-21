using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Diagnostics;

/// <summary>
/// The diagnostics reported by the NextUnit source generator.
/// </summary>
/// <remarks>
/// The IDs and message formats are a public suppression contract: users pin them in
/// <c>#pragma warning disable</c> and in .editorconfig severity overrides. Never renumber an ID or
/// reword a message format; add a new descriptor instead. The NEXTUNIT prefix is deliberately
/// distinct from the NU00xx analyzer family, and unifying the two is a separate, breaking decision.
/// </remarks>
internal static class GeneratorDiagnosticDescriptors
{
    private const string Category = "NextUnit";

    public static readonly DiagnosticDescriptor DependencyCycle = Error(
        "NEXTUNIT001",
        "Dependency cycle detected",
        "Test '{0}' has a circular dependency");

    public static readonly DiagnosticDescriptor UnresolvedDependency = Warning(
        "NEXTUNIT002",
        "Unresolved test dependency",
        "Test '{0}' depends on '{1}' which does not exist");

    public static readonly DiagnosticDescriptor ArgumentsWithTestData = Warning(
        "NEXTUNIT003",
        "Conflicting test data attributes",
        "Test '{0}' has both [Arguments] and [TestData] attributes. [Arguments] will be ignored and only [TestData] will be processed. Remove one of them to avoid confusion.");

    public static readonly DiagnosticDescriptor MatrixWithArguments = Error(
        "NEXTUNIT004",
        "Conflicting test data attributes",
        "Test '{0}' has both [Matrix] and [Arguments] attributes. Use only one approach for parameterizing tests.");

    public static readonly DiagnosticDescriptor MatrixWithTestData = Error(
        "NEXTUNIT005",
        "Conflicting test data attributes",
        "Test '{0}' has both [Matrix] and [TestData] attributes. Use only one approach for parameterizing tests.");

    public static readonly DiagnosticDescriptor IncompleteMatrixParameters = Error(
        "NEXTUNIT006",
        "Incomplete matrix parameters",
        "Test '{0}' has {1} parameters but only {2} have [Matrix] attributes. All parameters must have [Matrix] when using matrix tests.");

    public static readonly DiagnosticDescriptor MatrixExclusionValueCountMismatch = Error(
        "NEXTUNIT007",
        "Matrix exclusion parameter count mismatch",
        "Test '{0}' has [MatrixExclusion] with {1} values but the test has {2} matrix parameters.");

    public static readonly DiagnosticDescriptor ClassDataSourceWithOtherSources = Warning(
        "NEXTUNIT008",
        "Conflicting test data attributes",
        "Test '{0}' has [ClassDataSource] with other data source attributes. Only [ClassDataSource] will be processed.");

    public static readonly DiagnosticDescriptor MissingKeyForKeyedClassDataSource = Error(
        "NEXTUNIT009",
        "Missing Key for Keyed ClassDataSource",
        "Test '{0}' uses ClassDataSource with SharedType.Keyed but no Key is specified.");

    public static readonly DiagnosticDescriptor ParameterSourcesWithOtherSources = Warning(
        "NEXTUNIT010",
        "Conflicting test data attributes",
        "Test '{0}' uses parameter-level data sources ([Values], [ValuesFromMember], [ValuesFrom]) with other data source attributes. Only parameter-level sources will be processed.");

    public static readonly DiagnosticDescriptor IncompleteParameterDataSources = Error(
        "NEXTUNIT011",
        "Incomplete parameter data sources",
        "Test '{0}' has {1} parameters but only {2} have data source attributes ([Values], [ValuesFromMember], or [ValuesFrom]). All parameters must have a data source when using combined data sources (CancellationToken excluded).");

    public static readonly DiagnosticDescriptor MissingKeyForKeyedValuesFrom = Error(
        "NEXTUNIT012",
        "Missing Key for Keyed ValuesFrom",
        "Test '{0}' uses [ValuesFrom] with SharedType.Keyed on parameter '{1}' but no Key is specified.");

    /// <summary>
    /// Reported for a test method that would expand past the configured test case cap.
    /// </summary>
    /// <remarks>
    /// Not configurable, unlike every other rule here, because reporting it is only half of what the
    /// generator does: the method is also dropped from the registry, since leaving it in would run
    /// the expansion the cap exists to prevent. Suppressing the rule would therefore turn a failed
    /// build into a green one that silently omits those tests -- the shortened suite the whole
    /// feature is written to avoid. Raising the cap with
    /// <c>&lt;NextUnitMaxTestCasesPerMethod&gt;</c> is the escape hatch, so nothing is trapped by
    /// taking suppression away.
    /// </remarks>
    public static readonly DiagnosticDescriptor TestCaseExpansionLimitExceeded = Create(
        "NEXTUNIT013",
        "Test case expansion limit exceeded",
        "Test '{0}' expands to {1} test cases, which exceeds the limit of {2}. Reduce the [Matrix], [Arguments], [Repeat], or [Values] values, or raise the limit with <NextUnitMaxTestCasesPerMethod> in the project file.",
        DiagnosticSeverity.Error,
        WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>
    /// Reported for a lifecycle hook the generated registry cannot call.
    /// </summary>
    /// <remarks>
    /// Not configurable, for the reason <see cref="TestCaseExpansionLimitExceeded"/> is not: the
    /// hook is also dropped from the registry, because emitting a call the registry cannot make
    /// would replace this report with a <c>CS0122</c> in a file the user did not write. Suppressing
    /// the rule would therefore turn a failed build into a green one whose setup silently does not
    /// run -- the failure inherited hooks exist to remove.
    /// </remarks>
    public static readonly DiagnosticDescriptor LifecycleMethodNotAccessible = Create(
        "NEXTUNIT015",
        "Lifecycle method is not accessible to generated code",
        "Lifecycle method '{0}.{1}' is not accessible from the generated test registry; make it public, or internal in the test assembly.",
        DiagnosticSeverity.Error,
        WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>
    /// Reported for a type a test attribute names and the generated registry cannot.
    /// </summary>
    /// <remarks>
    /// Covers a display name formatter wherever it is declared, since no analyzer checks formatter
    /// accessibility, and a retry policy only when it is inherited, since <c>NU0016</c> reports a
    /// directly applied one already. Not configurable and the declaration is dropped, for the same
    /// reason as <see cref="LifecycleMethodNotAccessible"/>.
    /// </remarks>
    public static readonly DiagnosticDescriptor InheritedTypeNotAccessible = Create(
        "NEXTUNIT016",
        "Attribute type is not accessible to generated code",
        "Test '{0}' uses type '{1}', which is not accessible from the generated test registry; make it public, or internal in the test assembly.",
        DiagnosticSeverity.Error,
        WellKnownDiagnosticTags.NotConfigurable);

    private static DiagnosticDescriptor Error(string id, string title, string messageFormat) =>
        Create(id, title, messageFormat, DiagnosticSeverity.Error);

    private static DiagnosticDescriptor Warning(string id, string title, string messageFormat) =>
        Create(id, title, messageFormat, DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat,
        DiagnosticSeverity severity,
        params string[] customTags) =>
        new(id, title, messageFormat, Category, severity, isEnabledByDefault: true, customTags: customTags);
}
