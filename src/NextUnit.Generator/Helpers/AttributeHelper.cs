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

    /// <summary>
    /// The declarations a test method inherits an attribute from, nearest first.
    /// </summary>
    /// <remarks>
    /// The method own override chain first, then the containing type base chain -- the order C#
    /// itself resolves a member in, so a reader that takes the first level to answer implements
    /// "the nearest declaration wins" without a rule of its own. Levels are yielded as whole symbols
    /// rather than flattened into one attribute sequence because some readers must not merge across
    /// levels: <c>[Retry]</c> takes its whole declaration from one symbol, and flattening would let a
    /// policy on a base class attach itself to a plain budget on the derived one.
    /// <para>
    /// <c>System.Object</c> ends the type walk. Interfaces are not walked: an attribute on an
    /// implemented interface has no single nearest declaration when two interfaces carry it.
    /// </para>
    /// <para>
    /// The walk also stops at an error type and refuses to revisit a level. Neither is reachable
    /// from code the compiler accepted -- a circular base declaration is <c>CS0146</c> and Roslyn
    /// hands back an error type rather than a cycle, and an override chain is strictly ascending --
    /// but a generator runs against half-typed code on every keystroke, where the symbol graph is
    /// whatever recovery produced. Trusting the invariant was the alternative, and it makes a
    /// wrong guess cost an IDE hang rather than a wrong answer.
    /// </para>
    /// </remarks>
    private static IEnumerable<ISymbol> InheritanceLevels(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        for (IMethodSymbol? method = methodSymbol;
             method is not null && visited.Add(method);
             method = method.OverriddenMethod)
        {
            yield return method;
        }

        for (INamedTypeSymbol? type = typeSymbol;
             type is not null &&
                 type.SpecialType != SpecialType.System_Object &&
                 type.TypeKind != TypeKind.Error &&
                 visited.Add(type);
             type = type.BaseType)
        {
            yield return type;
        }
    }

    /// <summary>
    /// <see cref="InheritanceLevels"/> followed by the assembly, for the attributes that also
    /// declare <see cref="AttributeTargets.Assembly"/>.
    /// </summary>
    private static IEnumerable<ISymbol> InheritanceLevelsWithAssembly(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol)
    {
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            yield return level;
        }

        var assembly = typeSymbol.ContainingAssembly;
        if (assembly is not null)
        {
            yield return assembly;
        }
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
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            var result = GetExplicitFromSymbol(level);
            if (result.isExplicit)
            {
                return result;
            }
        }

        return (false, null);
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

    /// <summary>
    /// Collects every <c>[Category]</c> that applies to a test, nearest declaration first.
    /// </summary>
    /// <remarks>
    /// Accumulated rather than resolved, because the attribute allows multiple and a label is
    /// additive by nature: a base class saying "Integration" and a derived class saying "Slow" means
    /// both. Duplicates are kept -- a method and its class have always been able to declare the same
    /// category twice, and collapsing them is a separate change to what
    /// <c>ITestContext.Categories</c> reports. Nothing removes an inherited category; move the
    /// attribute down to the classes that want it.
    /// </remarks>
    public static EquatableArray<string> GetCategories(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol) =>
        CollectStrings(methodSymbol, typeSymbol, NextUnitAttributeNames.Category);

    /// <summary>
    /// Collects every <c>[Tag]</c> that applies to a test, on the same rule as
    /// <see cref="GetCategories"/>.
    /// </summary>
    public static EquatableArray<string> GetTags(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol) =>
        CollectStrings(methodSymbol, typeSymbol, NextUnitAttributeNames.Tag);

    private static EquatableArray<string> CollectStrings(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol,
        string attributeName)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            foreach (var attribute in level.GetAttributes())
            {
                if (!IsAttribute(attribute, attributeName))
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is string value &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    builder.Add(value);
                }
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
    /// reader expects it to apply; reading only the method and its class dropped it silently. It is
    /// the default a test inherits when neither nearer level declares one, not a ceiling over them:
    /// a class or method declaration replaces it, upward as readily as downward. The precedence
    /// matches <see cref="GetTimeout"/> and the culture attributes.
    /// </remarks>
    public static int? GetParallelLimit(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        foreach (var level in InheritanceLevelsWithAssembly(methodSymbol, typeSymbol))
        {
            var limit = GetParallelLimitFromSymbol(level);
            if (limit.HasValue)
            {
                return limit;
            }
        }

        return null;
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

            // A non-positive limit is read as "this level declared nothing", so the enclosing level
            // or the processor count still bounds the run. NU0019 reports it as a build error, so
            // reaching here means it was suppressed, and the reading matches a suppressed NU0018
            // culture name: inherit rather than abort. Carrying the value through would reach
            // ParallelOptions.MaxDegreeOfParallelism, where 0 and anything below -1 throw and abort
            // the whole run, and -1 means the processor count rather than the limit it looks like.
            if (value is int limit && limit > 0)
            {
                return limit;
            }
        }

        return null;
    }

    public static (bool notInParallel, EquatableArray<string> constraintKeys) GetNotInParallelInfo(IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol)
    {
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            var info = GetNotInParallelFromSymbol(level);
            if (info.HasValue)
            {
                return (true, info.Value);
            }
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
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            var group = GetParallelGroupFromSymbol(level);
            if (group is not null)
            {
                return group;
            }
        }

        return null;
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
        foreach (var level in InheritanceLevelsWithAssembly(methodSymbol, typeSymbol))
        {
            var timeout = GetTimeoutFromSymbol(level);
            if (timeout.HasValue)
            {
                return timeout;
            }
        }

        return null;
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

        foreach (var symbol in InheritanceLevelsWithAssembly(methodSymbol, typeSymbol))
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
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            var priority = GetExecutionPriorityFromSymbol(level);
            if (priority.HasValue)
            {
                return priority.Value;
            }
        }

        return 0;
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

    /// <summary>
    /// Resolves the retry budget and the flaky marking that apply to a test.
    /// </summary>
    /// <remarks>
    /// The whole retry declaration is taken from one level rather than merged property by property:
    /// a method that restates the count must not silently inherit the enclosing policy, which is the
    /// same rule the delay has always followed, and which now also keeps a base class policy from
    /// attaching itself to a derived class budget.
    /// <para>
    /// <c>[Flaky]</c> resolves differently because it is a marking, not a setting: a test is flaky
    /// when any level says so, and the reason is the nearest one that gives a reason. Nothing
    /// un-marks an inherited <c>[Flaky]</c>.
    /// </para>
    /// </remarks>
    public static (int? retryCount, int retryDelayMs, string? retryPolicyTypeName, bool isFlaky, string? flakyReason) GetRetryInfo(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol,
        SemanticModel? semanticModel)
    {
        var retry = default(RetryDeclaration);
        var retryIsInherited = false;
        var isFlaky = false;
        string? flakyReason = null;

        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            if (!retry.Count.HasValue)
            {
                retry = GetRetryFromSymbol(level);
                if (retry.Count.HasValue)
                {
                    retryIsInherited = !IsDeclaredHere(level, methodSymbol, typeSymbol);
                }
            }

            var (levelIsFlaky, levelReason) = GetFlakyFromSymbol(level);
            isFlaky |= levelIsFlaky;
            flakyReason ??= levelReason;
        }

        // Dropped only where it is also reported. NEXTUNIT016 covers the inherited case and is not
        // configurable, so dropping there trades a CS0122 in a file the user did not write for a
        // report that names the type. A directly applied policy is left alone on purpose: NU0016
        // reports that one and can be suppressed, and dropping a suppressed policy would silently
        // switch the test to the default retry behavior instead of the policy it asked for.
        var policyTypeName = FormatPolicyType(retry.PolicyType);
        if (retryIsInherited &&
            retry.PolicyType is not null &&
            !CanEmitType(retry.PolicyType, policyTypeName, semanticModel))
        {
            policyTypeName = null;
        }

        return (retry.Count, retry.DelayMs, policyTypeName, isFlaky, flakyReason);
    }

    /// <summary>
    /// Names a type the generated registry would emit but cannot reach, or <c>null</c> when every
    /// emitted type is in reach.
    /// </summary>
    /// <remarks>
    /// Two families reach here for different reasons. A display name formatter is checked wherever
    /// it is declared, because no analyzer covers formatter accessibility at all and the emitted
    /// <c>typeof</c> fails the same way for a directly applied one. A retry policy is checked only
    /// when it is inherited, because <c>NU0016</c> already reports a directly applied one -- and only
    /// an inherited declaration can name a type that was reachable in the assembly that wrote it and
    /// is not reachable here.
    /// </remarks>
    public static string? GetUnreachableEmittedTypeName(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol,
        SemanticModel? semanticModel)
    {
        var formatterType = ResolveDisplayNameFormatterType(methodSymbol, typeSymbol);
        if (formatterType is not null &&
            !CanEmitType(formatterType, formatterType.ToDisplayString(FullyQualifiedTypeFormat), semanticModel))
        {
            return formatterType.ToDisplayString(FullyQualifiedTypeFormat);
        }

        var policyType = ResolveInheritedRetryPolicyType(methodSymbol, typeSymbol);
        if (policyType is not null &&
            !CanEmitType(policyType, FormatPolicyType(policyType), semanticModel))
        {
            return policyType.ToDisplayString(FullyQualifiedTypeFormat);
        }

        return null;
    }

    /// <summary>
    /// Whether the registry can both reach a type and write the name it would emit for it.
    /// </summary>
    /// <remarks>
    /// Two questions, because a public type can still be unwritable: a reference brought in solely
    /// under an <c>extern alias</c>, or a generic argument that is, is invisible to the
    /// <c>global::</c>-rooted name the registry has to emit. Binding the name the emitter will
    /// actually write answers aliases, type arguments, and duplicate qualified names at once.
    /// </remarks>
    private static bool CanEmitType(ITypeSymbol type, string? typeExpression, SemanticModel? semanticModel) =>
        GeneratedRegistryAccess.CanReachType(type, semanticModel?.Compilation.Assembly) &&
        (typeExpression is null || GeneratedRegistryAccess.NameBindsToType(typeExpression, type, semanticModel));

    private static ITypeSymbol? ResolveInheritedRetryPolicyType(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol)
    {
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            var declaration = GetRetryFromSymbol(level);
            if (!declaration.Count.HasValue)
            {
                continue;
            }

            return IsDeclaredHere(level, methodSymbol, typeSymbol) ? null : declaration.PolicyType;
        }

        return null;
    }

    /// <summary>
    /// Whether a level is the test method or its class rather than something they inherit from.
    /// </summary>
    private static bool IsDeclaredHere(ISymbol level, IMethodSymbol methodSymbol, INamedTypeSymbol typeSymbol) =>
        SymbolEqualityComparer.Default.Equals(level, methodSymbol) ||
        SymbolEqualityComparer.Default.Equals(level, typeSymbol);

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
            var policyType = RetryAttributeMatcher.GetPolicyType(attribute);
            var isRetry = policyType is not null || RetryAttributeMatcher.IsPlainRetry(attribute);
            if (!isRetry || attribute.ConstructorArguments.Length == 0)
            {
                continue;
            }

            var count = attribute.ConstructorArguments[0].Value as int? ?? 1;
            var delayMs = attribute.ConstructorArguments.Length >= 2
                ? attribute.ConstructorArguments[1].Value as int? ?? 0
                : 0;

            if (policyType is not null)
            {
                return new RetryDeclaration(count, delayMs, policyType);
            }

            plain ??= new RetryDeclaration(count, delayMs, policyType: null);
        }

        return plain ?? new RetryDeclaration(count: null, delayMs: 0, policyType: null);
    }

    /// <summary>
    /// Formats a retry policy for the constructor call the registry emits.
    /// </summary>
    /// <remarks>
    /// Formatted for a constructor call, not for a type reference: <c>new global::Policy?()</c> is
    /// not valid C#, and <c>[Retry&lt;Policy?&gt;(2)]</c> is only a nullability warning at the
    /// attribute, so a consumer that does not promote warnings would otherwise get a hard error in
    /// generated code.
    /// </remarks>
    private static string? FormatPolicyType(ITypeSymbol? policyType) =>
        policyType?.ToDisplayString(TypeExpressionFormat);

    /// <summary>
    /// One symbol's retry declaration. A plain struct rather than a record: the generator targets
    /// netstandard2.0, which has no <c>IsExternalInit</c> for the synthesized init accessors.
    /// </summary>
    private readonly struct RetryDeclaration
    {
        public RetryDeclaration(int? count, int delayMs, ITypeSymbol? policyType)
        {
            Count = count;
            DelayMs = delayMs;
            PolicyType = policyType;
        }

        public int? Count { get; }
        public int DelayMs { get; }
        public ITypeSymbol? PolicyType { get; }
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

    /// <summary>
    /// Resolves the display name formatter that applies to a test, nearest declaration first.
    /// </summary>
    /// <remarks>
    /// A formatter the registry cannot name is dropped rather than emitted, for the reason
    /// <see cref="GetRetryInfo"/> drops an unreachable policy: <c>NEXTUNIT016</c> already fails the
    /// build, and the emitted <c>typeof</c> would bury it under a <c>CS0122</c>.
    /// </remarks>
    public static string? GetDisplayNameFormatterType(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol,
        SemanticModel? semanticModel)
    {
        var formatterType = ResolveDisplayNameFormatterType(methodSymbol, typeSymbol);
        if (formatterType is null)
        {
            return null;
        }

        var typeName = formatterType.ToDisplayString(FullyQualifiedTypeFormat);
        return CanEmitType(formatterType, typeName, semanticModel) ? typeName : null;
    }

    private static ITypeSymbol? ResolveDisplayNameFormatterType(
        IMethodSymbol methodSymbol,
        INamedTypeSymbol typeSymbol)
    {
        foreach (var level in InheritanceLevels(methodSymbol, typeSymbol))
        {
            if (GetDisplayNameFormatterFromSymbol(level) is { } formatter)
            {
                return formatter;
            }
        }

        return null;
    }

    private static ITypeSymbol? GetDisplayNameFormatterFromSymbol(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute, NextUnitAttributeNames.DisplayNameFormatter) &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol formatterType)
            {
                return formatterType;
            }

            var attrClass = attribute.AttributeClass;
            if (attrClass is { IsGenericType: true })
            {
                var constructedFrom = attrClass.ConstructedFrom;

                // Matched by arity-bearing metadata name and namespace rather than through
                // IsAttribute, because the constructed display string carries the type argument and
                // so never equals a stored name. The containing type must be absent and the
                // namespace must be NextUnit directly under the global one: a nested
                // NextUnit.Something.DisplayNameFormatterAttribute<T> reports the same metadata name
                // and the same enclosing namespace, so without both checks a user type that merely
                // looks like the attribute would supply the formatter.
                if (constructedFrom is
                    {
                        MetadataName: "DisplayNameFormatterAttribute`1",
                        ContainingType: null,
                        ContainingNamespace: { Name: NextUnitAttributeNames.Namespace, ContainingNamespace.IsGlobalNamespace: true }
                    })
                {
                    return attrClass.TypeArguments[0];
                }
            }
        }

        return null;
    }

}
