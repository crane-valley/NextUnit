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
        int? repeatCount)
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
