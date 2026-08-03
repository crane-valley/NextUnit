using NextUnit.CodeAnalysis.Shared;

namespace NextUnit.Generator.Models;

/// <summary>
/// Describes a test method discovered by the source generator.
/// </summary>
internal sealed record TestMethodDescriptor
{
    public TestMethodDescriptor(
        string id,
        string displayName,
        string fullyQualifiedTypeName,
        string methodName,
        bool notInParallel,
        EquatableArray<string> constraintKeys,
        string? parallelGroup,
        int? parallelLimit,
        EquatableArray<string> dependencies,
        EquatableArray<DependencyDescriptor> dependencyInfos,
        bool isSkipped,
        string? skipReason,
        bool isExplicit,
        string? explicitReason,
        EquatableArray<EquatableArray<ConstantValue>> argumentSets,
        EquatableArray<TestDataSource> testDataSources,
        EquatableArray<ClassDataSource> classDataSources,
        EquatableArray<ParameterDescriptor> parameters,
        EquatableArray<string> categories,
        EquatableArray<string> tags,
        bool isStatic,
        MethodReturnKind returnKind,
        bool acceptsCancellationToken,
        TestClassConstructorKind constructorKind,
        bool requiresTestOutput,
        bool requiresTestContext,
        int? timeoutMs,
        int? retryCount,
        int retryDelayMs,
        bool isFlaky,
        string? flakyReason,
        string? customDisplayName,
        string? displayNameFormatterType,
        int? repeatCount,
        EquatableArray<MatrixParameterDescriptor> matrixParameters,
        EquatableArray<MatrixExclusionDescriptor> matrixExclusions,
        EquatableArray<ParameterDataSourceDescriptor> combinedParameterSources,
        int priority)
    {
        Id = id;
        DisplayName = displayName;
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        MethodName = methodName;
        NotInParallel = notInParallel;
        ConstraintKeys = constraintKeys;
        ParallelGroup = parallelGroup;
        ParallelLimit = parallelLimit;
        Dependencies = dependencies;
        DependencyInfos = dependencyInfos;
        IsSkipped = isSkipped;
        SkipReason = skipReason;
        IsExplicit = isExplicit;
        ExplicitReason = explicitReason;
        ArgumentSets = argumentSets;
        TestDataSources = testDataSources;
        ClassDataSources = classDataSources;
        Parameters = parameters;
        Categories = categories;
        Tags = tags;
        IsStatic = isStatic;
        ReturnKind = returnKind;
        AcceptsCancellationToken = acceptsCancellationToken;
        ConstructorKind = constructorKind;
        RequiresTestOutput = requiresTestOutput;
        RequiresTestContext = requiresTestContext;
        TimeoutMs = timeoutMs;
        RetryCount = retryCount;
        RetryDelayMs = retryDelayMs;
        IsFlaky = isFlaky;
        FlakyReason = flakyReason;
        CustomDisplayName = customDisplayName;
        DisplayNameFormatterType = displayNameFormatterType;
        RepeatCount = repeatCount;
        MatrixParameters = matrixParameters;
        MatrixExclusions = matrixExclusions;
        CombinedParameterSources = combinedParameterSources;
        Priority = priority;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string FullyQualifiedTypeName { get; }
    public string MethodName { get; }
    public bool NotInParallel { get; }
    public EquatableArray<string> ConstraintKeys { get; }
    public string? ParallelGroup { get; }
    public int? ParallelLimit { get; }
    public EquatableArray<string> Dependencies { get; }
    public EquatableArray<DependencyDescriptor> DependencyInfos { get; }
    public bool IsSkipped { get; }
    public string? SkipReason { get; }
    public bool IsExplicit { get; }
    public string? ExplicitReason { get; }
    public EquatableArray<EquatableArray<ConstantValue>> ArgumentSets { get; }
    public EquatableArray<TestDataSource> TestDataSources { get; }
    public EquatableArray<ClassDataSource> ClassDataSources { get; }
    public EquatableArray<ParameterDescriptor> Parameters { get; }
    public EquatableArray<string> Categories { get; }
    public EquatableArray<string> Tags { get; }
    public bool IsStatic { get; }
    public MethodReturnKind ReturnKind { get; }
    public bool AcceptsCancellationToken { get; }
    public TestClassConstructorKind ConstructorKind { get; }
    public bool RequiresTestOutput { get; }
    public bool RequiresTestContext { get; }
    public int? TimeoutMs { get; }
    public int? RetryCount { get; }
    public int RetryDelayMs { get; }
    public bool IsFlaky { get; }
    public string? FlakyReason { get; }
    public string? CustomDisplayName { get; }
    public string? DisplayNameFormatterType { get; }
    public int? RepeatCount { get; }
    public EquatableArray<MatrixParameterDescriptor> MatrixParameters { get; }
    public EquatableArray<MatrixExclusionDescriptor> MatrixExclusions { get; }
    public EquatableArray<ParameterDataSourceDescriptor> CombinedParameterSources { get; }
    public int Priority { get; }
}

/// <summary>
/// Describes a lifecycle method (Before/After) discovered by the source generator.
/// </summary>
internal sealed record LifecycleMethodDescriptor
{
    public LifecycleMethodDescriptor(
        string fullyQualifiedTypeName,
        string methodName,
        EquatableArray<int> beforeScopes,
        EquatableArray<int> afterScopes,
        bool isStatic,
        MethodReturnKind returnKind,
        bool acceptsCancellationToken)
    {
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        MethodName = methodName;
        BeforeScopes = beforeScopes;
        AfterScopes = afterScopes;
        IsStatic = isStatic;
        ReturnKind = returnKind;
        AcceptsCancellationToken = acceptsCancellationToken;
    }

    public string FullyQualifiedTypeName { get; }
    public string MethodName { get; }
    public EquatableArray<int> BeforeScopes { get; }
    public EquatableArray<int> AfterScopes { get; }
    public bool IsStatic { get; }
    public MethodReturnKind ReturnKind { get; }
    public bool AcceptsCancellationToken { get; }
}

internal enum TestClassConstructorKind
{
    None,
    Parameterless,
    Context,
    Output,
    ContextAndOutput,
    OutputAndContext
}

internal enum DataSourceMemberKind
{
    Unknown,
    Method,
    Property,
    Field
}

/// <summary>
/// Describes a test data source for parameterized tests.
/// </summary>
internal sealed record TestDataSource
{
    public TestDataSource(
        string memberName,
        string? memberTypeName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape,
        bool acceptsCancellationToken,
        bool deferredEnumeration)
    {
        MemberName = memberName;
        MemberTypeName = memberTypeName;
        MemberKind = memberKind;
        Shape = shape;
        AcceptsCancellationToken = acceptsCancellationToken;
        DeferredEnumeration = deferredEnumeration;
    }

    public string MemberName { get; }
    public string? MemberTypeName { get; }
    public DataSourceMemberKind MemberKind { get; }

    /// <summary>
    /// Gets how the member hands over its rows, which decides whether the generator emits the
    /// synchronous provider delegate or the asynchronous one.
    /// </summary>
    public DataSourceShape Shape { get; }

    /// <summary>
    /// Gets a value indicating whether the member is a method that takes the discovery cancellation
    /// token. Only asynchronous sources can accept one; the synchronous provider delegate has no
    /// token to pass.
    /// </summary>
    public bool AcceptsCancellationToken { get; }

    /// <summary>
    /// Gets a value indicating whether the attribute asked for the rows to be enumerated during
    /// execution instead of during discovery.
    /// </summary>
    /// <remarks>
    /// Orthogonal to <see cref="Shape"/>: a deferred source is still emitted with whichever provider
    /// delegate its shape calls for, and the runtime decides when to invoke it.
    /// </remarks>
    public bool DeferredEnumeration { get; }
}

/// <summary>
/// Describes a test dependency including proceed-on-failure setting.
/// </summary>
internal sealed record DependencyDescriptor
{
    public DependencyDescriptor(string dependsOnId, bool proceedOnFailure)
    {
        DependsOnId = dependsOnId;
        ProceedOnFailure = proceedOnFailure;
    }

    public string DependsOnId { get; }
    public bool ProceedOnFailure { get; }
}

/// <summary>
/// Describes a matrix parameter with its possible values for Cartesian product generation.
/// </summary>
internal sealed record MatrixParameterDescriptor
{
    public MatrixParameterDescriptor(int parameterIndex, string parameterName, EquatableArray<ConstantValue> values)
    {
        ParameterIndex = parameterIndex;
        ParameterName = parameterName;
        Values = values;
    }

    public int ParameterIndex { get; }
    public string ParameterName { get; }
    public EquatableArray<ConstantValue> Values { get; }
}

/// <summary>
/// Describes a combination of values to exclude from matrix test generation.
/// </summary>
internal sealed record MatrixExclusionDescriptor
{
    public MatrixExclusionDescriptor(EquatableArray<ConstantValue> values)
    {
        Values = values;
    }

    public EquatableArray<ConstantValue> Values { get; }
}

/// <summary>
/// Describes a class-based data source for parameterized tests.
/// </summary>
internal sealed record ClassDataSource
{
    public ClassDataSource(string typeName, int sharedType, string? key)
    {
        TypeName = typeName;
        SharedType = sharedType;
        Key = key;
    }

    /// <summary>
    /// Gets the fully qualified type name of the data source class.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the sharing scope as an integer (maps to NextUnit.SharedType enum).
    /// </summary>
    public int SharedType { get; }

    /// <summary>
    /// Gets the key for keyed sharing (null if not applicable).
    /// </summary>
    public string? Key { get; }
}

/// <summary>
/// Specifies the kind of data source for a parameter.
/// </summary>
internal enum ParameterDataSourceKind
{
    /// <summary>
    /// Inline values from [Values] attribute.
    /// </summary>
    Inline,

    /// <summary>
    /// Values from a static member via [ValuesFromMember] attribute.
    /// </summary>
    Member,

    /// <summary>
    /// Values from a class data source via [ValuesFrom&lt;T&gt;] attribute.
    /// </summary>
    Class
}

/// <summary>
/// Describes a data source for a single parameter in a combined data source test.
/// </summary>
internal sealed record ParameterDataSourceDescriptor
{
    public ParameterDataSourceDescriptor(
        int parameterIndex,
        string parameterName,
        ParameterDataSourceKind kind,
        EquatableArray<ConstantValue> inlineValues,
        string? memberName,
        string? memberTypeName,
        DataSourceMemberKind memberKind,
        string? classTypeName,
        int sharedType,
        string? sharedKey)
    {
        ParameterIndex = parameterIndex;
        ParameterName = parameterName;
        Kind = kind;
        InlineValues = inlineValues;
        MemberName = memberName;
        MemberTypeName = memberTypeName;
        MemberKind = memberKind;
        ClassTypeName = classTypeName;
        SharedType = sharedType;
        SharedKey = sharedKey;
    }

    /// <summary>
    /// Gets the zero-based index of the parameter.
    /// </summary>
    public int ParameterIndex { get; }

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the kind of data source.
    /// </summary>
    public ParameterDataSourceKind Kind { get; }

    /// <summary>
    /// Gets the inline values for [Values] attribute.
    /// Empty for other kinds.
    /// </summary>
    public EquatableArray<ConstantValue> InlineValues { get; }

    /// <summary>
    /// Gets the member name for [ValuesFromMember] attribute.
    /// Null for other kinds.
    /// </summary>
    public string? MemberName { get; }

    /// <summary>
    /// Gets the fully qualified type name containing the member.
    /// Null if the test class should be used.
    /// </summary>
    public string? MemberTypeName { get; }
    public DataSourceMemberKind MemberKind { get; }

    /// <summary>
    /// Gets the fully qualified type name of the class data source.
    /// Null for non-class kinds.
    /// </summary>
    public string? ClassTypeName { get; }

    /// <summary>
    /// Gets the sharing scope as an integer (maps to NextUnit.SharedType enum).
    /// Only applicable for class data sources.
    /// </summary>
    public int SharedType { get; }

    /// <summary>
    /// Gets the key for keyed sharing.
    /// Only applicable when SharedType is Keyed.
    /// </summary>
    public string? SharedKey { get; }
}
