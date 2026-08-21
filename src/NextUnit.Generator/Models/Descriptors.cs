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
        string? retryPolicyTypeName,
        bool isFlaky,
        string? flakyReason,
        string? customDisplayName,
        string? displayNameFormatterType,
        int? repeatCount,
        EquatableArray<MatrixParameterDescriptor> matrixParameters,
        EquatableArray<MatrixExclusionDescriptor> matrixExclusions,
        EquatableArray<ParameterDataSourceDescriptor> combinedParameterSources,
        int priority,
        string? cultureName,
        string? uiCultureName,
        EquatableArray<LifecycleMethodDescriptor> inheritedLifecycleMethods,
        string? unreachableInheritedTypeName)
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
        RetryPolicyTypeName = retryPolicyTypeName;
        IsFlaky = isFlaky;
        FlakyReason = flakyReason;
        CustomDisplayName = customDisplayName;
        DisplayNameFormatterType = displayNameFormatterType;
        RepeatCount = repeatCount;
        MatrixParameters = matrixParameters;
        MatrixExclusions = matrixExclusions;
        CombinedParameterSources = combinedParameterSources;
        Priority = priority;
        CultureName = cultureName;
        UICultureName = uiCultureName;
        InheritedLifecycleMethods = inheritedLifecycleMethods;
        UnreachableInheritedTypeName = unreachableInheritedTypeName;
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
    public string? RetryPolicyTypeName { get; }
    public bool IsFlaky { get; }
    public string? FlakyReason { get; }
    public string? CustomDisplayName { get; }
    public string? DisplayNameFormatterType { get; }
    public int? RepeatCount { get; }
    public EquatableArray<MatrixParameterDescriptor> MatrixParameters { get; }
    public EquatableArray<MatrixExclusionDescriptor> MatrixExclusions { get; }
    public EquatableArray<ParameterDataSourceDescriptor> CombinedParameterSources { get; }
    public int Priority { get; }
    public string? CultureName { get; }
    public string? UICultureName { get; }

    /// <summary>
    /// Gets the lifecycle hooks the test class inherits from its base classes, base-most first.
    /// </summary>
    /// <remarks>
    /// Carried per test rather than joined against the <c>[Before]</c>/<c>[After]</c> providers in
    /// the emitter, because those providers are syntax based and see only this compilation. A base
    /// class in a referenced assembly -- a shared fixture package -- would otherwise keep failing in
    /// exactly the silent way this inheritance exists to remove. Walking the base chain here reads
    /// metadata symbols too, and it is also the only place the constructed base type is known, which
    /// <see cref="LifecycleMethodDescriptor.InvocationTypeName"/> needs.
    /// </remarks>
    public EquatableArray<LifecycleMethodDescriptor> InheritedLifecycleMethods { get; }

    /// <summary>
    /// Gets an inherited <c>[Retry&lt;TPolicy&gt;]</c> or <c>[DisplayNameFormatter]</c> type the
    /// generated registry cannot name, or <c>null</c> when nothing was dropped.
    /// </summary>
    /// <remarks>
    /// Kept as a string so it reaches the reported message without being emitted as a
    /// <c>typeof</c> or a <c>new</c>. A directly applied declaration is left to <c>NU0016</c> and
    /// <c>NU0022</c>, which see it in the compilation that wrote it; only an inherited one can carry
    /// a type that was reachable where it was declared and is not reachable here.
    /// </remarks>
    public string? UnreachableInheritedTypeName { get; }
}

/// <summary>
/// Describes a lifecycle method (Before/After) discovered by the source generator.
/// </summary>
internal sealed record LifecycleMethodDescriptor
{
    public LifecycleMethodDescriptor(
        string fullyQualifiedTypeName,
        string invocationTypeName,
        string methodName,
        string overrideRootId,
        bool isReachable,
        EquatableArray<int> beforeScopes,
        EquatableArray<int> afterScopes,
        bool isStatic,
        MethodReturnKind returnKind,
        bool acceptsCancellationToken)
    {
        FullyQualifiedTypeName = fullyQualifiedTypeName;
        InvocationTypeName = invocationTypeName;
        MethodName = methodName;
        OverrideRootId = overrideRootId;
        IsReachable = isReachable;
        BeforeScopes = beforeScopes;
        AfterScopes = afterScopes;
        IsStatic = isStatic;
        ReturnKind = returnKind;
        AcceptsCancellationToken = acceptsCancellationToken;
    }

    /// <summary>
    /// Gets the type that declares the hook.
    /// </summary>
    public string FullyQualifiedTypeName { get; }

    /// <summary>
    /// Gets the type the emitted delegate casts the instance to before calling the hook.
    /// </summary>
    /// <remarks>
    /// The declaring type, not the test class: a derived class that hides an annotated base hook
    /// with an unannotated <c>new</c> method would otherwise capture the call, and the hook the user
    /// annotated would never run. For a base declared as an open generic it is the constructed form
    /// the test class derives from -- <c>Base&lt;int&gt;</c> rather than <c>Base&lt;T&gt;</c> --
    /// because only the constructed form is valid in a cast.
    /// </remarks>
    public string InvocationTypeName { get; }

    public string MethodName { get; }

    /// <summary>
    /// Gets the identity of the C# override chain this declaration belongs to.
    /// </summary>
    /// <remarks>
    /// The signature of the base-most method in the chain, so a base declaration and a derived
    /// override of it collapse to one hook while a <c>new</c> method, which is a different method,
    /// stays its own hook. Matching by method name was rejected: it collapses hiding and overloads
    /// that C# keeps apart, and it separates nothing that C# joins.
    /// </remarks>
    public string OverrideRootId { get; }

    /// <summary>
    /// Gets whether the generated registry can call the hook.
    /// </summary>
    public bool IsReachable { get; }

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
        string? declaringTypeName,
        DataSourceMemberKind memberKind,
        DataSourceShape shape,
        string? rowTypeName,
        bool acceptsCancellationToken,
        bool deferredEnumeration,
        string? unreachableMemberTypeName = null)
    {
        MemberName = memberName;
        MemberTypeName = memberTypeName;
        DeclaringTypeName = declaringTypeName;
        MemberKind = memberKind;
        Shape = shape;
        RowTypeName = rowTypeName;
        AcceptsCancellationToken = acceptsCancellationToken;
        DeferredEnumeration = deferredEnumeration;
        UnreachableMemberTypeName = unreachableMemberTypeName;
    }

    public string MemberName { get; }
    public string? MemberTypeName { get; }

    /// <summary>
    /// Gets the type that declares the resolved member, which qualifies the emitted access, or
    /// <c>null</c> when no member was bound -- or when the declaring type has a name the generated
    /// file cannot bind, where the emitter keeps the type the attribute points at instead.
    /// </summary>
    /// <remarks>
    /// Deliberately a second type rather than a correction to <see cref="MemberTypeName"/>: that one
    /// is emitted as the descriptor's <c>DataSourceType</c>, which the runtime reads into the row id
    /// prefix. Moving it would rename every test case of an inherited source from
    /// <c>Derived.Rows</c> to <c>Base.Rows</c>, where filters and the VSTest adapter's
    /// id-to-descriptor mapping can both see it.
    /// <para>
    /// Qualifying by the declaring type is what stops another source generator from capturing the
    /// call. Generators cannot see each other's output, so a same-named member added to the same
    /// partial test class is invisible while this resolves and present once every generated source
    /// compiles together. Naming the type resolution bound against leaves that member two ways to
    /// go and no silent one: added to the declaring type it is a duplicate-member build error,
    /// added to any nearer type it is not what the emitted access names.
    /// </para>
    /// <para>
    /// It comes from the same <c>DataSourceMemberResolver</c> result the shape and the accessibility
    /// verdict do. A second lookup at the emitter would be a second precedence rule, free to drift
    /// from the one the analyzers validated. The name is always emittable when a member bound:
    /// <c>GeneratedRegistryAccess.CanReachMember</c> tests the containing type as part of the
    /// member, so a member out of reach is never bound in the first place.
    /// </para>
    /// </remarks>
    public string? DeclaringTypeName { get; }

    public DataSourceMemberKind MemberKind { get; }

    /// <summary>
    /// Gets the explicit <c>MemberType</c> that was dropped from <see cref="MemberTypeName"/>
    /// because the generated registry cannot name it, or <c>null</c> when nothing was dropped.
    /// </summary>
    /// <remarks>
    /// Kept as a string so that it reaches the emitted message without being emitted as a
    /// <c>typeof</c>. Telling this apart from a source that names no type at all is what stops the
    /// runtime falling back to the test class, where a same-named member would silently supply the
    /// wrong rows.
    /// </remarks>
    public string? UnreachableMemberTypeName { get; }

    /// <summary>
    /// Gets how the member hands over its rows, which decides whether the generator emits the
    /// synchronous provider delegate or the asynchronous one.
    /// </summary>
    public DataSourceShape Shape { get; }

    /// <summary>
    /// Gets the row type to name in the emitted adapter call, or <c>null</c> when inference already
    /// arrives at the selected one on its own.
    /// </summary>
    /// <remarks>
    /// Only a source offering more than one element type carries a value here. That is the case
    /// inference cannot resolve -- it reports <c>CS0411</c> in a file the user did not write -- and
    /// the only case where the arm the call reads can differ from the one
    /// <c>KnownDataSourceTypes.SelectRowType</c> chose and <c>NU0009</c> validated against. A source
    /// implementing the interface once is left inferred because the name buys nothing there and can
    /// still fail: a written type reaches nothing an <c>extern alias</c> hides, where inference
    /// needs no name at all.
    /// <para>
    /// It comes from the same <c>KnownDataSourceTypes.Classify</c> result <see cref="Shape"/> does.
    /// Recomputing it at the emitter was rejected: a second walk over the interfaces would be a
    /// second precedence rule, free to drift from the analyzers'.
    /// </para>
    /// </remarks>
    public string? RowTypeName { get; }

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
        string? declaringTypeName,
        DataSourceMemberKind memberKind,
        string? classTypeName,
        int sharedType,
        string? sharedKey,
        string? unreachableMemberTypeName = null)
    {
        ParameterIndex = parameterIndex;
        ParameterName = parameterName;
        Kind = kind;
        InlineValues = inlineValues;
        MemberName = memberName;
        MemberTypeName = memberTypeName;
        DeclaringTypeName = declaringTypeName;
        MemberKind = memberKind;
        ClassTypeName = classTypeName;
        SharedType = sharedType;
        SharedKey = sharedKey;
        UnreachableMemberTypeName = unreachableMemberTypeName;
    }

    /// <summary>
    /// Gets the explicit <c>MemberType</c> that was dropped from <see cref="MemberTypeName"/>
    /// because the generated registry cannot name it, or <c>null</c> when nothing was dropped.
    /// </summary>
    public string? UnreachableMemberTypeName { get; }

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

    /// <summary>
    /// Gets the type that declares the resolved member, which qualifies the emitted access, or
    /// <c>null</c> when no member was bound -- or when the declaring type has a name the generated
    /// file cannot bind, where the emitter keeps the type the attribute points at instead.
    /// </summary>
    /// <remarks>
    /// The parameter-level counterpart of <see cref="TestDataSource.DeclaringTypeName"/>, kept
    /// separate from <see cref="MemberTypeName"/> for the same reason: that one is emitted as the
    /// descriptor's <c>MemberType</c>, which the runtime reflection fallback searches from.
    /// </remarks>
    public string? DeclaringTypeName { get; }

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
