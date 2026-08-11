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

    /// <summary>
    /// Format for a type name emitted where a variable or cast type is expected. Keyword identifiers
    /// are escaped because the emitted text is parsed as C# rather than read by a human.
    /// </summary>
    public static readonly SymbolDisplayFormat FullyQualifiedTypeFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                                   SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// Format for a type name emitted inside <c>typeof(T)</c>, <c>new T()</c>, or a type argument
    /// list. Nullable reference annotations are dropped because <c>typeof</c> and <c>new</c> reject
    /// them outright, and a type argument carries no meaning from one. Keyword identifiers are
    /// escaped for the same reason as <see cref="FullyQualifiedTypeFormat"/>.
    /// </summary>
    public static readonly SymbolDisplayFormat TypeExpressionFormat =
        new(globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                   SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// Format for the type part of a test id. Keyword identifiers stay unescaped: an id is a string
    /// literal read by humans and matched by filters, never parsed as C#.
    /// </summary>
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

    public static DependencyMetadata GetDependencyMetadata(IMethodSymbol methodSymbol)
    {
        var dependencies = ImmutableArray.CreateBuilder<string>();
        var dependencyInfos = ImmutableArray.CreateBuilder<DependencyDescriptor>();
        var containingType = methodSymbol.ContainingType;
        var typeName = containingType.ToDisplayString(TestIdTypeFormat);

        void AddDependency(string name, bool proceedOnFailure)
        {
            var dependencyId = name.Contains('.') ? name : $"{typeName}.{name}";
            dependencies.Add(dependencyId);
            dependencyInfos.Add(new DependencyDescriptor(dependencyId, proceedOnFailure));
        }

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
                        AddDependency(name, proceedOnFailure);
                    }
                }
            }
            else if (argument.Value is string singleName && !string.IsNullOrWhiteSpace(singleName))
            {
                AddDependency(singleName, proceedOnFailure);
            }
        }

        return new DependencyMetadata(dependencies.ToImmutable(), dependencyInfos.ToImmutable());
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

    /// <summary>
    /// Resolves the parallel limit for a test from the method, its class, and its assembly.
    /// </summary>
    /// <remarks>
    /// The assembly level is read because <c>ParallelLimitAttribute</c> declares
    /// <see cref="AttributeTargets.Assembly"/>, so <c>[assembly: ParallelLimit(n)]</c> compiles and a
    /// reader expects it to bound the whole suite; reading only the method and its class dropped it
    /// silently. The precedence matches <see cref="GetTimeout"/> and the culture attributes.
    /// </remarks>
    public static int? GetParallelLimit(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var methodLimit = GetParallelLimitFromSymbol(methodSymbol);
        if (methodLimit.HasValue)
        {
            return methodLimit;
        }

        var classLimit = GetParallelLimitFromSymbol(typeSymbol);
        if (classLimit.HasValue)
        {
            return classLimit;
        }

        var assemblyLimit = GetParallelLimitFromSymbol(typeSymbol.ContainingAssembly);
        return assemblyLimit;
    }

    private static int? GetParallelLimitFromSymbol(ISymbol symbol)
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

    /// <summary>
    /// Resolves the cultures a test runs under from the method, its class, and its assembly.
    /// </summary>
    /// <remarks>
    /// Each axis resolves on its own, so a method that overrides only the current culture keeps the
    /// UI culture its class or assembly declared. Within one level an explicit
    /// <c>[Culture]</c>/<c>[UICulture]</c> wins over <c>[InvariantCulture]</c>, which fills in the
    /// axes that level left unspecified; that is what lets <c>[InvariantCulture]</c> combine with
    /// <c>[UICulture("ja-JP")]</c> to mean invariant formatting with Japanese resources.
    /// </remarks>
    public static (string? CultureName, string? UICultureName) GetCultureNames(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol)
    {
        string? cultureName = null;
        string? uiCultureName = null;

        foreach (var symbol in new ISymbol[] { methodSymbol, typeSymbol, typeSymbol.ContainingAssembly })
        {
            var isInvariant = HasAttribute(symbol, NextUnitAttributeNames.InvariantCulture);

            cultureName ??= GetCultureNameFromSymbol(symbol, NextUnitAttributeNames.Culture)
                ?? (isInvariant ? "" : null);
            uiCultureName ??= GetCultureNameFromSymbol(symbol, NextUnitAttributeNames.UICulture)
                ?? (isInvariant ? "" : null);

            if (cultureName is not null && uiCultureName is not null)
            {
                break;
            }
        }

        return (cultureName, uiCultureName);
    }

    private static string? GetCultureNameFromSymbol(ISymbol symbol, string attributeName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (!IsAttribute(attribute, attributeName))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            // The attribute's own ArgumentNullException never runs here, because nothing on this
            // path constructs the attribute, so NU0018 reports a null name instead - and the C#
            // nullable warning already flags it before that. Reaching this line therefore means both
            // were suppressed, and the safe reading is that the level declared nothing, exactly as a
            // suppressed NU0017 falls back to running the test once rather than aborting the run.
            // Carrying "declared, but unusable" through to the descriptors as a distinct state was
            // considered and rejected: it adds public surface to every descriptor to distinguish a
            // case that needs two deliberate suppressions to reach, and whose only consequence is
            // inheriting the enclosing declaration instead of overriding it.
            if (attribute.ConstructorArguments[0].Value is string name)
            {
                return name;
            }
        }

        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute, attributeName))
            {
                return true;
            }
        }

        return false;
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

    public static (int? retryCount, int retryDelayMs, string? retryPolicyTypeName, bool isFlaky, string? flakyReason) GetRetryInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        var methodRetry = GetRetryFromSymbol(methodSymbol);
        var classRetry = GetRetryFromSymbol(typeSymbol);

        // The whole retry declaration is taken from one symbol rather than merged property by
        // property: a method that restates the count must not silently inherit the class's policy,
        // which is the same rule the delay has always followed.
        var retry = methodRetry.Count.HasValue ? methodRetry : classRetry;

        var (methodIsFlaky, methodFlakyReason) = GetFlakyFromSymbol(methodSymbol);
        var (classIsFlaky, classFlakyReason) = GetFlakyFromSymbol(typeSymbol);

        var isFlaky = methodIsFlaky || classIsFlaky;
        var flakyReason = methodFlakyReason ?? classFlakyReason;

        return (retry.Count, retry.DelayMs, retry.PolicyTypeName, isFlaky, flakyReason);
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
                parameter.Type.ToDisplayString(TypeExpressionFormat),
                parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameter.Type.ToDisplayString(),
                parameter.Type.IsValueType));
        }

        return new EquatableArray<ParameterDescriptor>(builder.ToImmutable());
    }

    public static TestClassConstructorMetadata GetTestClassConstructorMetadata(INamedTypeSymbol typeSymbol)
    {
        var hasParameterless = false;
        var hasContext = false;
        var hasOutput = false;
        var requiresTestContext = false;
        var requiresTestOutput = false;
        TestClassConstructorKind? twoParameterKind = null;

        foreach (var constructor in typeSymbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var parameters = constructor.Parameters;
            foreach (var parameter in parameters)
            {
                var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                requiresTestContext |= parameterType == ITestContextTypeName;
                requiresTestOutput |= parameterType == ITestOutputTypeName;
            }

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
                if (twoParameterKind is null &&
                    first == ITestContextTypeName &&
                    second == ITestOutputTypeName)
                {
                    twoParameterKind = TestClassConstructorKind.ContextAndOutput;
                }

                if (twoParameterKind is null &&
                    first == ITestOutputTypeName &&
                    second == ITestContextTypeName)
                {
                    twoParameterKind = TestClassConstructorKind.OutputAndContext;
                }
            }
        }

        var kind = twoParameterKind ??
            (hasContext
                ? TestClassConstructorKind.Context
                : hasOutput
                    ? TestClassConstructorKind.Output
                    : hasParameterless
                        ? TestClassConstructorKind.Parameterless
                        : TestClassConstructorKind.None);

        return new TestClassConstructorMetadata(kind, requiresTestOutput, requiresTestContext);
    }

    /// <summary>
    /// Reads the retry declaration from one symbol, from either <c>[Retry]</c> or <c>[Retry&lt;TPolicy&gt;]</c>.
    /// </summary>
    /// <remarks>
    /// The first policy-bearing form wins when a symbol carries more than one retry attribute -- a
    /// policy alongside a plain budget, or two different policies. Both are reported as
    /// <c>NU0015</c>, but the analyzer can be suppressed and the generator still has to produce one
    /// deterministic answer; the more specific declaration, in declaration order, is the one to honor.
    /// </remarks>
    private static RetryDeclaration GetRetryFromSymbol(ISymbol symbol)
    {
        RetryDeclaration? plain = null;

        foreach (var attribute in symbol.GetAttributes())
        {
            var policyTypeName = GetRetryPolicyTypeName(attribute);
            var isRetry = policyTypeName is not null || RetryAttributeMatcher.IsPlainRetry(attribute);
            if (!isRetry || attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var count = attribute.ConstructorArguments[0].Value as int? ?? 1;
            var delayMs = attribute.ConstructorArguments.Length >= 2
                ? attribute.ConstructorArguments[1].Value as int? ?? 0
                : 0;

            if (policyTypeName is not null)
            {
                return new RetryDeclaration(count, delayMs, policyTypeName);
            }

            plain ??= new RetryDeclaration(count, delayMs, policyTypeName: null);
        }

        return plain ?? new RetryDeclaration(count: null, delayMs: 0, policyTypeName: null);
    }

    /// <summary>
    /// Returns the policy type of a <c>[Retry&lt;TPolicy&gt;]</c> attribute, or null for any other attribute.
    /// </summary>
    private static string? GetRetryPolicyTypeName(AttributeData attribute)
    {
        // Formatted for a constructor call, not for a type reference: `new global::Policy?()` is not
        // valid C#, and `[Retry<Policy?>(2)]` is only a nullability warning at the attribute, so a
        // consumer that does not promote warnings would otherwise get a hard error in generated code.
        return RetryAttributeMatcher.GetPolicyType(attribute)?.ToDisplayString(TypeExpressionFormat);
    }

    /// <summary>
    /// One symbol's retry declaration. A plain struct rather than a record: the generator targets
    /// netstandard2.0, which has no <c>IsExternalInit</c> for the synthesized init accessors.
    /// </summary>
    private readonly struct RetryDeclaration
    {
        public RetryDeclaration(int? count, int delayMs, string? policyTypeName)
        {
            Count = count;
            DelayMs = delayMs;
            PolicyTypeName = policyTypeName;
        }

        public int? Count { get; }
        public int DelayMs { get; }
        public string? PolicyTypeName { get; }
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

}
