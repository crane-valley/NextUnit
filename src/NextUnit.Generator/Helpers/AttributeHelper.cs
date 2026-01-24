using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

/// <summary>
/// Helper methods for extracting attribute information from symbols.
/// </summary>
internal static class AttributeHelper
{
    public const string TestAttributeMetadataName = "global::NextUnit.TestAttribute";
    public const string BeforeAttributeMetadataName = "global::NextUnit.BeforeAttribute";
    public const string AfterAttributeMetadataName = "global::NextUnit.AfterAttribute";
    public const string NotInParallelMetadataName = "global::NextUnit.NotInParallelAttribute";
    public const string ParallelGroupMetadataName = "global::NextUnit.ParallelGroupAttribute";
    public const string ParallelLimitMetadataName = "global::NextUnit.ParallelLimitAttribute";
    public const string DependsOnMetadataName = "global::NextUnit.DependsOnAttribute";
    public const string SkipAttributeMetadataName = "global::NextUnit.SkipAttribute";
    public const string ArgumentsAttributeMetadataName = "global::NextUnit.ArgumentsAttribute";
    public const string TestDataAttributeMetadataName = "global::NextUnit.TestDataAttribute";
    public const string CategoryAttributeMetadataName = "global::NextUnit.CategoryAttribute";
    public const string TagAttributeMetadataName = "global::NextUnit.TagAttribute";
    public const string TimeoutAttributeMetadataName = "global::NextUnit.TimeoutAttribute";
    public const string RetryAttributeMetadataName = "global::NextUnit.RetryAttribute";
    public const string FlakyAttributeMetadataName = "global::NextUnit.FlakyAttribute";
    public const string RepeatAttributeMetadataName = "global::NextUnit.RepeatAttribute";
    public const string DisplayNameAttributeMetadataName = "global::NextUnit.DisplayNameAttribute";
    public const string DisplayNameFormatterAttributeMetadataName = "global::NextUnit.DisplayNameFormatterAttribute";
    public const string MatrixAttributeMetadataName = "global::NextUnit.MatrixAttribute";
    public const string MatrixExclusionAttributeMetadataName = "global::NextUnit.MatrixExclusionAttribute";
    public const string ClassDataSourceAttributePrefix = "ClassDataSourceAttribute`";
    public const string ITestOutputMetadataName = "global::NextUnit.Core.ITestOutput";
    public const string ITestContextMetadataName = "global::NextUnit.Core.ITestContext";

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

    public static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        return symbol.GetAttributes().Any(attribute => IsAttribute(attribute, metadataName));
    }

    public static bool IsAttribute(AttributeData attribute, string metadataName)
    {
        return attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == metadataName;
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

    public static ImmutableArray<string> GetDependencies(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, DependsOnMetadataName))
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

    public static ImmutableArray<DependencyDescriptor> GetDependencyInfos(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<DependencyDescriptor>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        // Build fully-qualified dependency ID from method name
        string BuildDependencyId(string name) =>
            name.Contains('.') ? name : $"{typeName}.{name}";

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, DependsOnMetadataName))
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
            if (!IsAttribute(attribute, SkipAttributeMetadataName))
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

    public static ImmutableArray<ImmutableArray<TypedConstant>> GetArgumentSets(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<ImmutableArray<TypedConstant>>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, ArgumentsAttributeMetadataName))
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
                builder.Add(argsArray.Values);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<TestDataSource> GetTestDataSources(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<TestDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, TestDataAttributeMetadataName))
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

            string? memberTypeName = memberTypeArg?.ToDisplayString(FullyQualifiedTypeFormat);

            builder.Add(new TestDataSource(memberName, memberTypeName));
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<ClassDataSource> GetClassDataSources(IMethodSymbol methodSymbol)
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
            if (!metadataName.StartsWith(ClassDataSourceAttributePrefix, StringComparison.Ordinal) ||
                constructedFrom.ContainingNamespace.ToDisplayString() != "NextUnit")
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

    public static ImmutableArray<string> GetCategories(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, CategoryAttributeMetadataName))
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
            if (!IsAttribute(attribute, CategoryAttributeMetadataName))
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

    public static ImmutableArray<string> GetTags(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, TagAttributeMetadataName))
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
            if (!IsAttribute(attribute, TagAttributeMetadataName))
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
            if (!IsAttribute(attribute, ParallelLimitMetadataName))
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

    public static (bool notInParallel, ImmutableArray<string> constraintKeys) GetNotInParallelInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
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
            if (!IsAttribute(attribute, NotInParallelMetadataName))
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
            if (!IsAttribute(attribute, ParallelGroupMetadataName))
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

    public static ImmutableArray<int> GetLifecycleScopes(IMethodSymbol methodSymbol, string attributeMetadataName)
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
            if (!IsAttribute(attribute, TimeoutAttributeMetadataName))
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
            if (!IsAttribute(attribute, RepeatAttributeMetadataName))
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

    public static ImmutableArray<MatrixParameterDescriptor> GetMatrixParameters(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<MatrixParameterDescriptor>();

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];

            foreach (var attribute in parameter.GetAttributes())
            {
                if (!IsAttribute(attribute, MatrixAttributeMetadataName))
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
                    builder.Add(new MatrixParameterDescriptor(i, parameter.Name, valuesArg.Values));
                }
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<MatrixExclusionDescriptor> GetMatrixExclusions(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<MatrixExclusionDescriptor>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!IsAttribute(attribute, MatrixExclusionAttributeMetadataName))
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
                builder.Add(new MatrixExclusionDescriptor(valuesArg.Values));
            }
        }

        return builder.ToImmutable();
    }

    private static (int? count, int delayMs) GetRetryFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, RetryAttributeMetadataName))
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
            if (!IsAttribute(attribute, FlakyAttributeMetadataName))
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
            if (!IsAttribute(attribute, DisplayNameAttributeMetadataName))
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
            if (IsAttribute(attribute, DisplayNameFormatterAttributeMetadataName) &&
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
                    constructedFrom.ContainingNamespace.ToDisplayString() == "NextUnit")
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
                if (parameterType == ITestOutputMetadataName)
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
                if (parameterType == ITestContextMetadataName)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
