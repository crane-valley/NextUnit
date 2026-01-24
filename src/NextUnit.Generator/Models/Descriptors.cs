using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Models;

/// <summary>
/// Describes a test method discovered by the source generator.
/// </summary>
internal sealed class TestMethodDescriptor
{
    public TestMethodDescriptor(
        string id,
        string displayName,
        string fullyQualifiedTypeName,
        string methodName,
        bool notInParallel,
        ImmutableArray<string> constraintKeys,
        string? parallelGroup,
        int? parallelLimit,
        ImmutableArray<string> dependencies,
        ImmutableArray<DependencyDescriptor> dependencyInfos,
        bool isSkipped,
        string? skipReason,
        ImmutableArray<ImmutableArray<TypedConstant>> argumentSets,
        ImmutableArray<TestDataSource> testDataSources,
        ImmutableArray<ClassDataSource> classDataSources,
        ImmutableArray<IParameterSymbol> parameters,
        ImmutableArray<string> categories,
        ImmutableArray<string> tags,
        bool isStatic,
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
        ImmutableArray<MatrixParameterDescriptor> matrixParameters,
        ImmutableArray<MatrixExclusionDescriptor> matrixExclusions)
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
        ArgumentSets = argumentSets;
        TestDataSources = testDataSources;
        ClassDataSources = classDataSources;
        Parameters = parameters;
        Categories = categories;
        Tags = tags;
        IsStatic = isStatic;
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
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string FullyQualifiedTypeName { get; }
    public string MethodName { get; }
    public bool NotInParallel { get; }
    public ImmutableArray<string> ConstraintKeys { get; }
    public string? ParallelGroup { get; }
    public int? ParallelLimit { get; }
    public ImmutableArray<string> Dependencies { get; }
    public ImmutableArray<DependencyDescriptor> DependencyInfos { get; }
    public bool IsSkipped { get; }
    public string? SkipReason { get; }
    public ImmutableArray<ImmutableArray<TypedConstant>> ArgumentSets { get; }
    public ImmutableArray<TestDataSource> TestDataSources { get; }
    public ImmutableArray<ClassDataSource> ClassDataSources { get; }
    public ImmutableArray<IParameterSymbol> Parameters { get; }
    public ImmutableArray<string> Categories { get; }
    public ImmutableArray<string> Tags { get; }
    public bool IsStatic { get; }
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
    public ImmutableArray<MatrixParameterDescriptor> MatrixParameters { get; }
    public ImmutableArray<MatrixExclusionDescriptor> MatrixExclusions { get; }
}

/// <summary>
/// Describes a lifecycle method (Before/After) discovered by the source generator.
/// </summary>
internal sealed class LifecycleMethodDescriptor
{
    public LifecycleMethodDescriptor(
        string fullyQualifiedTypeName,
        string methodName,
        ImmutableArray<int> beforeScopes,
        ImmutableArray<int> afterScopes,
        bool isStatic)
    {
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        MethodName = methodName;
        BeforeScopes = beforeScopes;
        AfterScopes = afterScopes;
        IsStatic = isStatic;
    }

    public string FullyQualifiedTypeName { get; }
    public string MethodName { get; }
    public ImmutableArray<int> BeforeScopes { get; }
    public ImmutableArray<int> AfterScopes { get; }
    public bool IsStatic { get; }
}

/// <summary>
/// Describes a test data source for parameterized tests.
/// </summary>
internal sealed class TestDataSource
{
    public TestDataSource(string memberName, string? memberTypeName)
    {
        MemberName = memberName;
        MemberTypeName = memberTypeName;
    }

    public string MemberName { get; }
    public string? MemberTypeName { get; }
}

/// <summary>
/// Describes a test dependency including proceed-on-failure setting.
/// </summary>
internal sealed class DependencyDescriptor
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
internal sealed class MatrixParameterDescriptor
{
    public MatrixParameterDescriptor(int parameterIndex, string parameterName, ImmutableArray<TypedConstant> values)
    {
        ParameterIndex = parameterIndex;
        ParameterName = parameterName;
        Values = values;
    }

    public int ParameterIndex { get; }
    public string ParameterName { get; }
    public ImmutableArray<TypedConstant> Values { get; }
}

/// <summary>
/// Describes a combination of values to exclude from matrix test generation.
/// </summary>
internal sealed class MatrixExclusionDescriptor
{
    public MatrixExclusionDescriptor(ImmutableArray<TypedConstant> values)
    {
        Values = values;
    }

    public ImmutableArray<TypedConstant> Values { get; }
}

/// <summary>
/// Describes a class-based data source for parameterized tests.
/// </summary>
internal sealed class ClassDataSource
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
