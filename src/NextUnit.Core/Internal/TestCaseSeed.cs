using System.Diagnostics.CodeAnalysis;

namespace NextUnit.Internal;

/// <summary>
/// Carries the descriptor state that every runtime expansion copies onto the test cases it produces.
/// </summary>
/// <remarks>
/// The three expanders used to repeat the same ~20 property assignments, so a new descriptor property
/// had to be wired up in every one of them. Routing all of them through this type keeps the projection
/// into <see cref="TestCaseDescriptor"/> in a single place; only the per-row values (id, arguments,
/// display name, row-level skip and labels) stay at the call sites.
/// </remarks>
internal sealed class TestCaseSeed
{
    public TestCaseSeed(TestDataDescriptor descriptor)
    {
        TestClass = descriptor.TestClass;
        MethodName = descriptor.MethodName;
        ParameterTypes = descriptor.ParameterTypes;
        TestClassFactory = descriptor.TestClassFactory;
        TestMethodWithArguments = descriptor.TestMethodWithArguments;
        Lifecycle = descriptor.Lifecycle;
        Parallel = descriptor.Parallel;
        Dependencies = descriptor.Dependencies;
        DependencyInfos = descriptor.DependencyInfos;
        IsSkipped = descriptor.IsSkipped;
        SkipReason = descriptor.SkipReason;
        IsExplicit = descriptor.IsExplicit;
        ExplicitReason = descriptor.ExplicitReason;
        Categories = descriptor.Categories;
        Tags = descriptor.Tags;
        RequiresTestOutput = descriptor.RequiresTestOutput;
        RequiresTestContext = descriptor.RequiresTestContext;
        TimeoutMs = descriptor.TimeoutMs;
        Retry = descriptor.Retry;
        Culture = descriptor.Culture;
        CustomDisplayNameTemplate = descriptor.CustomDisplayNameTemplate;
        DisplayNameFormatterType = descriptor.DisplayNameFormatterType;
        Priority = descriptor.Priority;
    }

    public TestCaseSeed(ClassDataSourceDescriptor descriptor)
    {
        TestClass = descriptor.TestClass;
        MethodName = descriptor.MethodName;
        ParameterTypes = descriptor.ParameterTypes;
        TestClassFactory = descriptor.TestClassFactory;
        TestMethodWithArguments = descriptor.TestMethodWithArguments;
        Lifecycle = descriptor.Lifecycle;
        Parallel = descriptor.Parallel;
        Dependencies = descriptor.Dependencies;
        DependencyInfos = descriptor.DependencyInfos;
        IsSkipped = descriptor.IsSkipped;
        SkipReason = descriptor.SkipReason;
        IsExplicit = descriptor.IsExplicit;
        ExplicitReason = descriptor.ExplicitReason;
        Categories = descriptor.Categories;
        Tags = descriptor.Tags;
        RequiresTestOutput = descriptor.RequiresTestOutput;
        RequiresTestContext = descriptor.RequiresTestContext;
        TimeoutMs = descriptor.TimeoutMs;
        Retry = descriptor.Retry;
        Culture = descriptor.Culture;
        CustomDisplayNameTemplate = descriptor.CustomDisplayNameTemplate;
        DisplayNameFormatterType = descriptor.DisplayNameFormatterType;
        Priority = descriptor.Priority;
    }

    public TestCaseSeed(CombinedDataSourceDescriptor descriptor)
    {
        TestClass = descriptor.TestClass;
        MethodName = descriptor.MethodName;
        ParameterTypes = descriptor.ParameterTypes;
        TestClassFactory = descriptor.TestClassFactory;
        TestMethodWithArguments = descriptor.TestMethodWithArguments;
        Lifecycle = descriptor.Lifecycle;
        Parallel = descriptor.Parallel;
        Dependencies = descriptor.Dependencies;
        DependencyInfos = descriptor.DependencyInfos;
        IsSkipped = descriptor.IsSkipped;
        SkipReason = descriptor.SkipReason;
        IsExplicit = descriptor.IsExplicit;
        ExplicitReason = descriptor.ExplicitReason;
        Categories = descriptor.Categories;
        Tags = descriptor.Tags;
        RequiresTestOutput = descriptor.RequiresTestOutput;
        RequiresTestContext = descriptor.RequiresTestContext;
        TimeoutMs = descriptor.TimeoutMs;
        Retry = descriptor.Retry;
        Culture = descriptor.Culture;
        CustomDisplayNameTemplate = descriptor.CustomDisplayNameTemplate;
        DisplayNameFormatterType = descriptor.DisplayNameFormatterType;
        Priority = descriptor.Priority;
    }

    // Annotated with exactly what TestCaseDescriptor.TestClass and the reflection invoker fallback need,
    // so descriptors carrying wider annotations can still flow into this seed.
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
    public Type TestClass { get; }

    public string MethodName { get; }

    public Type[] ParameterTypes { get; }

    public TestClassFactoryDelegate? TestClassFactory { get; }

    public TestMethodWithArgumentsDelegate? TestMethodWithArguments { get; }

    public LifecycleInfo Lifecycle { get; }

    public ParallelInfo Parallel { get; }

    public IReadOnlyList<TestCaseId> Dependencies { get; }

    public IReadOnlyList<DependencyInfo> DependencyInfos { get; }

    public bool IsSkipped { get; }

    public string? SkipReason { get; }

    public bool IsExplicit { get; }

    public string? ExplicitReason { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<string> Tags { get; }

    public bool RequiresTestOutput { get; }

    public bool RequiresTestContext { get; }

    public int? TimeoutMs { get; }

    public RetryInfo Retry { get; }

    public TestCultureInfo Culture { get; }

    public string? CustomDisplayNameTemplate { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type? DisplayNameFormatterType { get; }

    public int Priority { get; }

    /// <summary>
    /// Resolves the invoker used for every test case expanded from this descriptor, falling back to
    /// reflection when the generator could not emit a delegate.
    /// </summary>
    public TestMethodWithArgumentsDelegate? ResolveTestInvoker() =>
        TestMethodWithArguments ??
        ReflectionTestInvokerFactory.Create(TestClass, MethodName, ParameterTypes);

    /// <summary>
    /// Builds one expanded test case.
    /// </summary>
    /// <param name="testId">The unique test case identifier built by the expander.</param>
    /// <param name="arguments">The arguments for this expansion.</param>
    /// <param name="argumentSetIndex">The zero-based index of this expansion.</param>
    /// <param name="testMethod">The invoker returned by <see cref="ResolveTestInvoker"/>.</param>
    /// <param name="row">
    /// The resolved data row when the expansion came from a data row, or <c>null</c> when the arguments
    /// have no row-level display name, skip reason, or labels to merge.
    /// </param>
    /// <param name="repeatIndex">
    /// The zero-based <c>[Repeat]</c> iteration this case is, or <c>null</c> when the test carries no
    /// <see cref="NextUnit.RepeatAttribute"/>.
    /// </param>
    /// <remarks>
    /// The repeat suffix on the display name is appended here rather than by the caller so that the
    /// runtime expansion reads the same as the generated one: <c>TestCaseEmitter</c> appends
    /// <c>" (Repeat #n)"</c> after the parameterized name is built, including over a row-supplied
    /// name, and a suffix applied before the formatter ran would sort and group differently.
    /// </remarks>
    public TestCaseDescriptor CreateTestCase(
        string testId,
        object?[] arguments,
        int argumentSetIndex,
        TestMethodWithArgumentsDelegate? testMethod,
        ResolvedTestDataRow? row = null,
        int? repeatIndex = null)
    {
        var displayName = row?.DisplayName ?? DisplayNameBuilder.Build(
            MethodName,
            CustomDisplayNameTemplate,
            DisplayNameFormatterType,
            TestClass,
            arguments,
            argumentSetIndex);

        if (repeatIndex is { } iteration)
        {
            displayName = $"{displayName} (Repeat #{iteration + 1})";
        }

        return new TestCaseDescriptor
        {
            Id = new TestCaseId(testId),
            DisplayName = displayName,
            TestClass = TestClass,
            MethodName = MethodName,
            TestMethodWithArguments = testMethod,
            TestClassFactory = TestClassFactory,
            Lifecycle = Lifecycle,
            Parallel = Parallel,
            Dependencies = Dependencies,
            DependencyInfos = DependencyInfos,
            IsSkipped = IsSkipped || row?.SkipReason is not null,
            SkipReason = SkipReason ?? row?.SkipReason,
            IsExplicit = IsExplicit,
            ExplicitReason = ExplicitReason,
            Arguments = arguments,
            Categories = row is null ? Categories : TestDataRowResolver.MergeLabels(Categories, row.Value.Categories),
            Tags = row is null ? Tags : TestDataRowResolver.MergeLabels(Tags, row.Value.Tags),
            RequiresTestOutput = RequiresTestOutput,
            RequiresTestContext = RequiresTestContext,
            TimeoutMs = TimeoutMs,
            Retry = Retry,
            Culture = Culture,
            CustomDisplayNameTemplate = CustomDisplayNameTemplate,
            DisplayNameFormatterType = DisplayNameFormatterType,
            Priority = Priority,
            RepeatIndex = repeatIndex
        };
    }

    /// <summary>
    /// Builds the single test case that stands in for a deferred data source until execution
    /// expands it.
    /// </summary>
    /// <param name="testId">The placeholder identifier built by the expander.</param>
    /// <param name="source">The descriptor the placeholder expands back into.</param>
    /// <remarks>
    /// Deliberately carries no invoker and no arguments: a placeholder is replaced by its rows
    /// before the dependency graph is built, so it never reaches execution. The one exception is a
    /// source whose test is skipped, which is reported straight from the placeholder rather than
    /// running a data source whose rows nobody will execute.
    /// </remarks>
    public TestCaseDescriptor CreateDeferredPlaceholder(string testId, TestDataDescriptor source)
    {
        return new TestCaseDescriptor
        {
            Id = new TestCaseId(testId),
            DisplayName = $"{source.DisplayName} (deferred data source: {source.DataSourceName})",
            TestClass = TestClass,
            MethodName = MethodName,
            TestClassFactory = TestClassFactory,
            Lifecycle = Lifecycle,
            Parallel = Parallel,
            Dependencies = Dependencies,
            DependencyInfos = DependencyInfos,
            IsSkipped = IsSkipped,
            SkipReason = SkipReason,
            IsExplicit = IsExplicit,
            ExplicitReason = ExplicitReason,
            Categories = Categories,
            Tags = Tags,
            RequiresTestOutput = RequiresTestOutput,
            RequiresTestContext = RequiresTestContext,
            TimeoutMs = TimeoutMs,
            Retry = Retry,
            Culture = Culture,
            CustomDisplayNameTemplate = CustomDisplayNameTemplate,
            DisplayNameFormatterType = DisplayNameFormatterType,
            Priority = Priority,
            DeferredDataSource = source
        };
    }
}
