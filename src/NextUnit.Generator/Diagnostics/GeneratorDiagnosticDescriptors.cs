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
    /// Reported when <c>&lt;NextUnitMaxTestCasesPerMethod&gt;</c> is set to something that cannot be
    /// used as a cap.
    /// </summary>
    /// <remarks>
    /// Not configurable, for the reason <see cref="TestCaseExpansionLimitExceeded"/> is and one more:
    /// suppressing it would restore exactly the fail-open behavior it replaces, where a typo meant to
    /// tighten the cap loosened it to the default instead. Nothing is trapped by taking suppression
    /// away, because correcting the value or deleting the property both resolve it, and deleting it
    /// is what the default was always for.
    /// <para>
    /// Reported rather than silently rounded to the nearest usable cap: there is no reading of
    /// <c>100O</c> that yields a number the user asked for, and guessing one on a bound that governs
    /// how much a compilation may expand is the failure mode this rule exists to remove.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor TestCaseExpansionLimitOverrideUnusable = Create(
        "NEXTUNIT014",
        "Test case expansion limit override is unusable",
        "The <NextUnitMaxTestCasesPerMethod> value '{0}' is not a positive 32-bit integer. Set it to a value between 1 and 2147483647, or remove the property to use the default limit of {1}.",
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

    /// <summary>
    /// Reported for a <c>[Before]</c> or <c>[After]</c> hook declared as an explicit interface
    /// implementation.
    /// </summary>
    /// <remarks>
    /// Its own rule rather than <see cref="LifecycleMethodNotAccessible"/>, which it replaces for
    /// this shape, because that message names an edit C# rejects: an explicit implementation takes
    /// no accessibility modifier at all (<c>CS0106</c>), so "make it public" cannot be applied. The
    /// remedy is the declaration form -- an ordinary method implementing the member implicitly.
    /// <para>
    /// Reported at the declaration, whether or not anything derives from it, which is the one place
    /// the lifecycle rules do not wait for a use site. A consumer cannot report it: a compilation
    /// imports metadata with <c>MetadataImportOptions.Public</c>, so an explicit implementation on a
    /// base class in a referenced assembly is not among that type's members at all. The declaring
    /// assembly is the last compilation in which the hook is visible, so it is the last place the
    /// silence can be broken.
    /// </para>
    /// <para>
    /// Calling the hook through a cast to its interface was the rejected alternative. It leaves the
    /// referenced-assembly case exactly as silent, since the member is still not imported; an
    /// interface cast dispatches on the runtime type's interface map, so a derived class that
    /// re-implements the interface captures the call the base declaration was annotated for --
    /// the capture the cast to the declaring type exists to prevent; and a static explicit
    /// implementation cannot be called through an interface without a generic constraint the
    /// registry has no type parameter to put it on.
    /// </para>
    /// <para>
    /// Not configurable and the hook is dropped, for the reason
    /// <see cref="LifecycleMethodNotAccessible"/> is both.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor LifecycleMethodIsExplicitInterfaceImplementation = Create(
        "NEXTUNIT017",
        "Lifecycle method is an explicit interface implementation",
        "Lifecycle method '{1}' on '{0}' is an explicit interface implementation, which the generated test registry cannot call. Declare it as an ordinary method -- public, or internal in the test assembly -- so it implements the interface member implicitly.",
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
