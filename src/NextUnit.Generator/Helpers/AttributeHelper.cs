using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NextUnit.CodeAnalysis.Shared;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Helper methods for extracting attribute information from symbols.
/// </summary>
internal static class AttributeHelper
{
    // Constructor parameter types are matched against FullyQualifiedFormat display strings
    // directly rather than through IsAttribute, so these two carry the global:: prefix.
    public const string ITestOutputTypeName =
        NextUnitAttributeNames.GlobalPrefix + NextUnitAttributeNames.ITestOutput;

    public const string ITestContextTypeName =
        NextUnitAttributeNames.GlobalPrefix + NextUnitAttributeNames.ITestContext;

    public static readonly SymbolDisplayFormat FullyQualifiedTypeFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Format for typeof() expressions - excludes nullable reference type annotations since C# typeof() does not support them.
    /// </summary>
    public static readonly SymbolDisplayFormat TypeofCompatibleFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static readonly SymbolDisplayFormat TestIdTypeFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Matches an attribute against a fully qualified NextUnit attribute name.
    /// </summary>
    /// <param name="attribute">The attribute to test.</param>
    /// <param name="fullName">
    /// The fully qualified name without the <c>global::</c> prefix, as declared in
    /// <see cref="NextUnitAttributeNames"/>.
    /// </param>
    public static bool IsAttribute(AttributeData attribute, string fullName)
    {
        // FullyQualifiedFormat emits the global:: prefix, so it is prepended here instead of
        // storing a second, separately drifting spelling of every attribute name.
        return attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            == NextUnitAttributeNames.GlobalPrefix + fullName;
    }

    public static string CreateTestId(IMethodSymbol methodSymbol)
    {
        var typeName = methodSymbol.ContainingType.ToDisplayString(TestIdTypeFormat);
        return $"{typeName}.{methodSymbol.Name}";
    }

    public static string GetFullyQualifiedTypeName(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    public static string ToLiteral(string value)
    {
        return SymbolDisplay.FormatLiteral(value, true);
    }

    public static EquatableArray<string> GetDependencies(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.DependsOn))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];

            if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (var value in argument.Values)
                {
                    if (value.Value is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        var dependencyId = name.Contains('.') ? name : $"{typeName}.{name}";
                        builder.Add(dependencyId);
                    }
                }
            }
            else if (argument.Value is string singleName && !string.IsNullOrWhiteSpace(singleName))
            {
                var dependencyId = singleName.Contains('.') ? singleName : $"{typeName}.{singleName}";
                builder.Add(dependencyId);
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<DependencyDescriptor> GetDependencyInfos(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<DependencyDescriptor>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        // Build fully-qualified dependency ID from method name
        string BuildDependencyId(string name) =>
            name.Contains('.') ? name : $"{typeName}.{name}";

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.DependsOn))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            // Get ProceedOnFailure named argument
            var proceedOnFailure = attribute.NamedArguments
                .Where(arg => arg.Key == "ProceedOnFailure" && arg.Value.Value is bool)
                .Select(arg => (bool)arg.Value.Value!)
                .FirstOrDefault();

            var argument = attribute.ConstructorArguments[0];

            if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (var value in argument.Values)
                {
                    if (value.Value is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        builder.Add(new DependencyDescriptor(BuildDependencyId(name), proceedOnFailure));
                    }
                }
            }
            else if (argument.Value is string singleName && !string.IsNullOrWhiteSpace(singleName))
            {
                builder.Add(new DependencyDescriptor(BuildDependencyId(singleName), proceedOnFailure));
            }
        }

        return builder.ToImmutable();
    }

    public static (bool isSkipped, string? skipReason) GetSkipInfo(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Skip))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                return (true, null);
            }

            var reasonArg = attribute.ConstructorArguments[0];
            if (reasonArg.Value is string reason)
            {
                return (true, reason);
            }

            return (true, null);
        }

        return (false, null);
    }

    /// <summary>
    /// Gets explicit test information from the method or its containing type.
    /// </summary>
    /// <param name="methodSymbol">The test method symbol.</param>
    /// <param name="typeSymbol">The containing type symbol.</param>
    /// <returns>A tuple indicating if the test is explicit and the optional reason.</returns>
    public static (bool isExplicit, string? explicitReason) GetExplicitInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        // Check method-level attribute first
        var methodResult = GetExplicitFromSymbol(methodSymbol);
        if (methodResult.isExplicit)
        {
            return methodResult;
        }

        // Check class-level attribute
        return GetExplicitFromSymbol(typeSymbol);
    }

    private static (bool isExplicit, string? explicitReason) GetExplicitFromSymbol(ISymbol symbol)
    {
        var explicitAttribute = symbol.GetAttributes()
            .FirstOrDefault(attr => IsAttribute(attr, NextUnitAttributeNames.Explicit));

        if (explicitAttribute is null)
        {
            return (false, null);
        }

        if (explicitAttribute.ConstructorArguments.Length > 0 &&
            explicitAttribute.ConstructorArguments[0].Value is string reason)
        {
            return (true, reason);
        }

        return (true, null);
    }

    public static EquatableArray<EquatableArray<ConstantValue>> GetArgumentSets(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<EquatableArray<ConstantValue>>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Arguments))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var argsArray = attribute.ConstructorArguments[0];
            if (argsArray.Kind == TypedConstantKind.Array)
            {
                builder.Add(ConstantValueFactory.CreateRange(argsArray.Values));
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<TestDataSource> GetTestDataSources(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<TestDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.TestData))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not string memberName ||
                string.IsNullOrEmpty(memberName))
            {
                continue;
            }

            var memberTypeArg = attribute.NamedArguments
                .Where(arg => arg.Key == "MemberType" && arg.Value.Value is INamedTypeSymbol)
                .Select(arg => (INamedTypeSymbol)arg.Value.Value!)
                .FirstOrDefault();

            var memberType = memberTypeArg ?? methodSymbol.ContainingType;
            string? memberTypeName = memberTypeArg?.ToDisplayString(FullyQualifiedTypeFormat);

            builder.Add(new TestDataSource(
                memberName,
                memberTypeName,
                GetDataSourceMemberKind(memberType, memberName)));
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<ClassDataSource> GetClassDataSources(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<ClassDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is not { IsGenericType: true })
            {
                continue;
            }

            var constructedFrom = attrClass.ConstructedFrom;
            var metadataName = constructedFrom.MetadataName;

            // Check if it's a ClassDataSourceAttribute<T> variant (T1 through T4)
            if (!metadataName.StartsWith(NextUnitAttributeNames.MetadataNames.ClassDataSourceAttributePrefix, StringComparison.Ordinal) ||
                constructedFrom.ContainingNamespace.ToDisplayString() != NextUnitAttributeNames.Namespace)
            {
                continue;
            }

            // Extract Shared and Key named arguments
            var sharedType = 0; // SharedType.None
            var key = (string?)null;

            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Shared" && namedArg.Value.Value is int sharedValue)
                {
                    sharedType = sharedValue;
                }
                else if (namedArg.Key == "Key" && namedArg.Value.Value is string keyValue)
                {
                    key = keyValue;
                }
            }

            // Extract all type arguments from the generic attribute
            // Use TypeofCompatibleFormat to exclude nullable annotations (typeof() doesn't support them)
            foreach (var typeArg in attrClass.TypeArguments)
            {
                var typeName = typeArg.ToDisplayString(TypeofCompatibleFormat);
                builder.Add(new ClassDataSource(typeName, sharedType, key));
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<string> GetCategories(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Category))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string categoryName &&
                !string.IsNullOrWhiteSpace(categoryName))
            {
                builder.Add(categoryName);
            }
        }

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Category))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string categoryName &&
                !string.IsNullOrWhiteSpace(categoryName))
            {
                builder.Add(categoryName);
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<string> GetTags(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Tag))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string tagName &&
                !string.IsNullOrWhiteSpace(tagName))
            {
                builder.Add(tagName);
            }
        }

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Tag))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string tagName &&
                !string.IsNullOrWhiteSpace(tagName))
            {
                builder.Add(tagName);
            }
        }

        return builder.ToImmutable();
    }

    public static int? GetParallelLimit(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.ParallelLimit))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var value = attribute.ConstructorArguments[0].Value;

            if (value is int limit)
            {
                return limit;
            }
        }

        return null;
    }

    public static (bool notInParallel, EquatableArray<string> constraintKeys) GetNotInParallelInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        // Method-level takes precedence
        var methodInfo = GetNotInParallelFromSymbol(methodSymbol);
        if (methodInfo.HasValue)
        {
            return (true, methodInfo.Value);
        }

        // Fall back to class-level
        var classInfo = GetNotInParallelFromSymbol(typeSymbol);
        if (classInfo.HasValue)
        {
            return (true, classInfo.Value);
        }

        return (false, ImmutableArray<string>.Empty);
    }

    private static ImmutableArray<string>? GetNotInParallelFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.NotInParallel))
            {
                continue;
            }

            // NotInParallelAttribute can have no arguments (fully serial)
            // or params string[] constraintKeys
            if (attribute.ConstructorArguments.Length == 0)
            {
                return ImmutableArray<string>.Empty;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Kind == TypedConstantKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<string>();
                foreach (var value in argument.Values)
                {
                    if (value.Value is string key && !string.IsNullOrWhiteSpace(key))
                    {
                        builder.Add(key);
                    }
                }
                return builder.ToImmutable();
            }
        }

        return null;
    }

    public static string? GetParallelGroup(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        // Method-level takes precedence
        var methodGroup = GetParallelGroupFromSymbol(methodSymbol);
        if (methodGroup is not null)
        {
            return methodGroup;
        }

        // Fall back to class-level
        return GetParallelGroupFromSymbol(typeSymbol);
    }

    private static string? GetParallelGroupFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.ParallelGroup))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string groupName &&
                !string.IsNullOrWhiteSpace(groupName))
            {
                return groupName;
            }
        }

        return null;
    }

    public static EquatableArray<int> GetLifecycleScopes(IMethodSymbol methodSymbol, string attributeMetadataName)
    {
        var builder = ImmutableArray.CreateBuilder<int>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, attributeMetadataName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var value = attribute.ConstructorArguments[0].Value;

            if (value is int scope)
            {
                builder.Add(scope);
            }
        }

        return builder.ToImmutable();
    }

    public static int? GetTimeout(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var methodTimeout = GetTimeoutFromSymbol(methodSymbol);
        if (methodTimeout.HasValue)
        {
            return methodTimeout;
        }

        var classTimeout = GetTimeoutFromSymbol(typeSymbol);
        if (classTimeout.HasValue)
        {
            return classTimeout;
        }

        var assemblyTimeout = GetTimeoutFromSymbol(typeSymbol.ContainingAssembly);
        return assemblyTimeout;
    }

    private static int? GetTimeoutFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Timeout))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is int timeout)
            {
                return timeout;
            }
        }

        return null;
    }

    public static int GetExecutionPriority(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var methodPriority = GetExecutionPriorityFromSymbol(methodSymbol);
        if (methodPriority.HasValue)
        {
            return methodPriority.Value;
        }

        var classPriority = GetExecutionPriorityFromSymbol(typeSymbol);
        return classPriority ?? 0;
    }

    private static int? GetExecutionPriorityFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.ExecutionPriority))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is int priority)
            {
                return priority;
            }
        }

        return null;
    }

    public static (int? retryCount, int retryDelayMs, bool isFlaky, string? flakyReason) GetRetryInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var (methodRetryCount, methodRetryDelayMs) = GetRetryFromSymbol(methodSymbol);
        var (classRetryCount, classRetryDelayMs) = GetRetryFromSymbol(typeSymbol);

        var retryCount = methodRetryCount ?? classRetryCount;
        var retryDelayMs = methodRetryCount.HasValue ? methodRetryDelayMs : classRetryDelayMs;

        var (methodIsFlaky, methodFlakyReason) = GetFlakyFromSymbol(methodSymbol);
        var (classIsFlaky, classFlakyReason) = GetFlakyFromSymbol(typeSymbol);

        var isFlaky = methodIsFlaky || classIsFlaky;
        var flakyReason = methodFlakyReason ?? classFlakyReason;

        return (retryCount, retryDelayMs, isFlaky, flakyReason);
    }

    public static int? GetRepeatCount(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Repeat))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is int count && count > 0)
            {
                return count;
            }
        }

        return null;
    }

    public static EquatableArray<MatrixParameterDescriptor> GetMatrixParameters(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<MatrixParameterDescriptor>();

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];

            foreach (var attribute in parameter.GetAttributes())
            {
                if (!IsAttribute(attribute, NextUnitAttributeNames.Matrix))
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length == 0)
                {
                    continue;
                }

                var valuesArg = attribute.ConstructorArguments[0];
                if (valuesArg.Kind == TypedConstantKind.Array)
                {
                    builder.Add(new MatrixParameterDescriptor(i, parameter.Name, ConstantValueFactory.CreateRange(valuesArg.Values)));
                }
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<MatrixExclusionDescriptor> GetMatrixExclusions(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<MatrixExclusionDescriptor>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.MatrixExclusion))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var valuesArg = attribute.ConstructorArguments[0];
            if (valuesArg.Kind == TypedConstantKind.Array)
            {
                builder.Add(new MatrixExclusionDescriptor(ConstantValueFactory.CreateRange(valuesArg.Values)));
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Extracts combined parameter sources from method parameters.
    /// Returns non-empty array only if at least one parameter has [Values], [ValuesFromMember], or [ValuesFrom&lt;T&gt;].
    /// </summary>
    public static EquatableArray<ParameterDataSourceDescriptor> GetCombinedParameterSources(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<ParameterDataSourceDescriptor>();
        var hasAnySource = false;

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];
            var descriptor = TryGetParameterDataSource(parameter, i);

            if (descriptor is not null)
            {
                hasAnySource = true;
                builder.Add(descriptor);
            }
        }

        // Only return sources if at least one parameter has a data source attribute
        return hasAnySource
            ? new EquatableArray<ParameterDataSourceDescriptor>(builder.ToImmutable())
            : EquatableArray<ParameterDataSourceDescriptor>.Empty;
    }

    private static ParameterDataSourceDescriptor? TryGetParameterDataSource(IParameterSymbol parameter, int index)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            // Check for [Values]
            if (IsAttribute(attribute, NextUnitAttributeNames.Values) &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Kind == TypedConstantKind.Array)
            {
                return new ParameterDataSourceDescriptor(
                    parameterIndex: index,
                    parameterName: parameter.Name,
                    kind: ParameterDataSourceKind.Inline,
                    inlineValues: ConstantValueFactory.CreateRange(attribute.ConstructorArguments[0].Values),
                    memberName: null,
                    memberTypeName: null,
                    memberKind: DataSourceMemberKind.Unknown,
                    classTypeName: null,
                    sharedType: 0,
                    sharedKey: null);
            }

            // Check for [ValuesFromMember]
            if (IsAttribute(attribute, NextUnitAttributeNames.ValuesFromMember) &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string memberName &&
                !string.IsNullOrEmpty(memberName))
            {
                var memberTypeArg = attribute.NamedArguments
                    .Where(arg => arg.Key == "MemberType")
                    .Select(arg => arg.Value.Value as INamedTypeSymbol)
                    .FirstOrDefault(t => t is not null);

                var memberType = memberTypeArg ?? parameter.ContainingSymbol.ContainingType;
                string? memberTypeName = memberTypeArg?.ToDisplayString(TypeofCompatibleFormat);

                return new ParameterDataSourceDescriptor(
                    parameterIndex: index,
                    parameterName: parameter.Name,
                    kind: ParameterDataSourceKind.Member,
                    inlineValues: EquatableArray<ConstantValue>.Empty,
                    memberName: memberName,
                    memberTypeName: memberTypeName,
                    memberKind: GetDataSourceMemberKind(memberType, memberName),
                    classTypeName: null,
                    sharedType: 0,
                    sharedKey: null);
            }

            // Check for [ValuesFrom<T>]
            var attrClass = attribute.AttributeClass;
            if (attrClass is { IsGenericType: true })
            {
                var constructedFrom = attrClass.ConstructedFrom;
                var metadataName = constructedFrom.MetadataName;

                if (metadataName.StartsWith(NextUnitAttributeNames.MetadataNames.ValuesFromAttributePrefix, StringComparison.Ordinal) &&
                    constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitAttributeNames.Namespace)
                {
                    var typeArg = attrClass.TypeArguments[0];
                    var classTypeName = typeArg.ToDisplayString(TypeofCompatibleFormat);

                    // Extract Shared and Key named arguments
                    var sharedType = 0; // SharedType.None
                    var key = (string?)null;

                    foreach (var namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "Shared" && namedArg.Value.Value is int sharedValue)
                        {
                            sharedType = sharedValue;
                        }
                        else if (namedArg.Key == "Key" && namedArg.Value.Value is string keyValue)
                        {
                            key = keyValue;
                        }
                    }

                    return new ParameterDataSourceDescriptor(
                        parameterIndex: index,
                        parameterName: parameter.Name,
                        kind: ParameterDataSourceKind.Class,
                        inlineValues: EquatableArray<ConstantValue>.Empty,
                        memberName: null,
                        memberTypeName: null,
                        memberKind: DataSourceMemberKind.Unknown,
                        classTypeName: classTypeName,
                        sharedType: sharedType,
                        sharedKey: key);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the method parameters into value models so the pipeline never carries parameter symbols.
    /// </summary>
    public static EquatableArray<ParameterDescriptor> GetParameters(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length == 0)
        {
            return EquatableArray<ParameterDescriptor>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ParameterDescriptor>(methodSymbol.Parameters.Length);

        foreach (var parameter in methodSymbol.Parameters)
        {
            builder.Add(new ParameterDescriptor(
                parameter.Name,
                parameter.Type.ToDisplayString(TypeofCompatibleFormat),
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameter.Type.ToDisplayString(),
                parameter.Type.IsValueType));
        }

        return new EquatableArray<ParameterDescriptor>(builder.ToImmutable());
    }

    public static TestClassConstructorKind GetTestClassConstructorKind(INamedTypeSymbol typeSymbol)
    {
        var hasParameterless = false;
        var hasContext = false;
        var hasOutput = false;

        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var parameters = constructor.Parameters;
            if (parameters.Length == 0)
            {
                hasParameterless = true;
                continue;
            }

            if (parameters.Length == 1)
            {
                var parameterType = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                hasContext |= parameterType == ITestContextTypeName;
                hasOutput |= parameterType == ITestOutputTypeName;
                continue;
            }

            if (parameters.Length == 2)
            {
                var first = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var second = parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (first == ITestContextTypeName && second == ITestOutputTypeName)
                {
                    return TestClassConstructorKind.ContextAndOutput;
                }

                if (first == ITestOutputTypeName && second == ITestContextTypeName)
                {
                    return TestClassConstructorKind.OutputAndContext;
                }
            }
        }

        if (hasContext)
        {
            return TestClassConstructorKind.Context;
        }

        if (hasOutput)
        {
            return TestClassConstructorKind.Output;
        }

        return hasParameterless
            ? TestClassConstructorKind.Parameterless
            : TestClassConstructorKind.None;
    }

    private static DataSourceMemberKind GetDataSourceMemberKind(
        INamedTypeSymbol? typeSymbol,
        string memberName)
    {
        if (typeSymbol is null)
        {
            return DataSourceMemberKind.Unknown;
        }

        foreach (var member in typeSymbol.GetMembers(memberName))
        {
            if (!member.IsStatic)
            {
                continue;
            }

            if (member is IMethodSymbol { Parameters.Length: 0 })
            {
                return DataSourceMemberKind.Method;
            }

            if (member is IPropertySymbol)
            {
                return DataSourceMemberKind.Property;
            }

            if (member is IFieldSymbol)
            {
                return DataSourceMemberKind.Field;
            }
        }

        return DataSourceMemberKind.Unknown;
    }

    private static (int? count, int delayMs) GetRetryFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Retry))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var count = attribute.ConstructorArguments[0].Value as int? ?? 1;
            var delayMs = attribute.ConstructorArguments.Length >= 2
                ? attribute.ConstructorArguments[1].Value as int? ?? 0
                : 0;

            return (count, delayMs);
        }

        return (null, 0);
    }

    private static (bool isFlaky, string? reason) GetFlakyFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.Flaky))
            {
                continue;
            }

            var reason = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            return (true, reason);
        }

        return (false, null);
    }

    public static string? GetCustomDisplayName(IMethodSymbol methodSymbol)
    {
        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, NextUnitAttributeNames.DisplayName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string displayName)
            {
                return displayName;
            }
        }

        return null;
    }

    public static string? GetDisplayNameFormatterType(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var methodFormatter = GetDisplayNameFormatterFromSymbol(methodSymbol);
        if (methodFormatter is not null)
        {
            return methodFormatter;
        }

        return GetDisplayNameFormatterFromSymbol(typeSymbol);
    }

    private static string? GetDisplayNameFormatterFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute, NextUnitAttributeNames.DisplayNameFormatter) &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol formatterType)
            {
                return formatterType.ToDisplayString(FullyQualifiedTypeFormat);
            }

            var attrClass = attribute.AttributeClass;
            if (attrClass is { IsGenericType: true })
            {
                var constructedFrom = attrClass.ConstructedFrom;
                if (constructedFrom.MetadataName == "DisplayNameFormatterAttribute`1" &&
                    constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitAttributeNames.Namespace)
                {
                    var typeArg = attrClass.TypeArguments[0];
                    return typeArg.ToDisplayString(FullyQualifiedTypeFormat);
                }
            }
        }

        return null;
    }

    public static bool RequiresTestOutput(INamedTypeSymbol typeSymbol)
    {
        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            foreach (var parameter in constructor.Parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (parameterType == ITestOutputTypeName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool RequiresTestContext(INamedTypeSymbol typeSymbol)
    {
        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            foreach (var parameter in constructor.Parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (parameterType == ITestContextTypeName)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
