using System.Collections.Immutable;

namespace NextUnit.CodeAnalysis.Shared;

/// <summary>
/// NextUnit attribute and type names shared by the source generator and the analyzers.
/// </summary>
/// <remarks>
/// The names are stored in exactly one spelling: the fully qualified name without the
/// <c>global::</c> prefix, which is what <c>ISymbol.ToDisplayString()</c> produces. The generator
/// compares against <c>SymbolDisplayFormat.FullyQualifiedFormat</c>, which does emit the prefix,
/// so it prepends <see cref="GlobalPrefix"/> at the comparison site rather than storing a second
/// spelling of every name here. Keeping one spelling is the point of this file: the three
/// previously duplicated spellings (global::-prefixed, unprefixed, bare simple names) could drift
/// independently.
/// </remarks>
internal static class NextUnitAttributeNames
{
    /// <summary>
    /// Prefix emitted by <c>SymbolDisplayFormat.FullyQualifiedFormat</c>.
    /// </summary>
    public const string GlobalPrefix = "global::";

    /// <summary>
    /// Namespace that declares the NextUnit attributes.
    /// </summary>
    public const string Namespace = "NextUnit";

    public const string Test = "NextUnit.TestAttribute";
    public const string Before = "NextUnit.BeforeAttribute";
    public const string After = "NextUnit.AfterAttribute";
    public const string NotInParallel = "NextUnit.NotInParallelAttribute";
    public const string ParallelGroup = "NextUnit.ParallelGroupAttribute";
    public const string ParallelLimit = "NextUnit.ParallelLimitAttribute";
    public const string DependsOn = "NextUnit.DependsOnAttribute";
    public const string Skip = "NextUnit.SkipAttribute";
    public const string Explicit = "NextUnit.ExplicitAttribute";
    public const string Arguments = "NextUnit.ArgumentsAttribute";
    public const string TestData = "NextUnit.TestDataAttribute";
    public const string Category = "NextUnit.CategoryAttribute";
    public const string Tag = "NextUnit.TagAttribute";
    public const string Timeout = "NextUnit.TimeoutAttribute";
    public const string Retry = "NextUnit.RetryAttribute";
    public const string Flaky = "NextUnit.FlakyAttribute";
    public const string Repeat = "NextUnit.RepeatAttribute";
    public const string DisplayName = "NextUnit.DisplayNameAttribute";
    public const string DisplayNameFormatter = "NextUnit.DisplayNameFormatterAttribute";
    public const string Matrix = "NextUnit.MatrixAttribute";
    public const string MatrixExclusion = "NextUnit.MatrixExclusionAttribute";
    public const string Values = "NextUnit.ValuesAttribute";
    public const string ValuesFromMember = "NextUnit.ValuesFromMemberAttribute";
    public const string ExecutionPriority = "NextUnit.ExecutionPriorityAttribute";
    public const string ITestOutput = "NextUnit.Core.ITestOutput";
    public const string ITestContext = "NextUnit.Core.ITestContext";

    /// <summary>
    /// The attributes that mark a method as a test or a lifecycle hook, and therefore constrain
    /// its signature.
    /// </summary>
    public static readonly ImmutableHashSet<string> TestAndLifecycle =
        ImmutableHashSet.Create(Test, Before, After);

    /// <summary>
    /// Simple attribute names, used where matching runs on every method in a compilation and the
    /// cost of formatting a fully qualified display string per attribute is not worth paying.
    /// </summary>
    public static class SimpleNames
    {
        public const string Test = "TestAttribute";
        public const string Arguments = "ArgumentsAttribute";

        /// <summary>
        /// Shared by <c>[Retry]</c> and <c>[Retry&lt;TPolicy&gt;]</c>: <c>ISymbol.Name</c> carries no
        /// generic arity, so the two are told apart by arity rather than by name.
        /// </summary>
        public const string Retry = "RetryAttribute";

        public const string TestData = "TestDataAttribute";
        public const string ClassDataSource = "ClassDataSourceAttribute";
        public const string Matrix = "MatrixAttribute";
        public const string Values = "ValuesAttribute";
        public const string ValuesFromMember = "ValuesFromMemberAttribute";
        public const string ValuesFrom = "ValuesFromAttribute";
    }

    /// <summary>
    /// Metadata names of the generic NextUnit types. Generic arity is part of the metadata name,
    /// so these are matched by prefix or by exact metadata name rather than by display string.
    /// </summary>
    public static class MetadataNames
    {
        public const string ClassDataSourceAttributePrefix = "ClassDataSourceAttribute`";
        public const string ValuesFromAttributePrefix = "ValuesFromAttribute`";
        public const string TestDataRow = "TestDataRow`1";
    }
}
