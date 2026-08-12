using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NextUnit.CodeAnalysis.Shared;
using NextUnit.Generator.Models;

namespace NextUnit.Generator.Helpers;

internal static class DataSourceAttributeReader
{
    public static EquatableArray<EquatableArray<ConstantValue>> GetArgumentSets(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<EquatableArray<ConstantValue>>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.Arguments))
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

    public static EquatableArray<TestDataSource> GetTestDataSources(
        IMethodSymbol methodSymbol,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var builder = ImmutableArray.CreateBuilder<TestDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            if (!AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.TestData))
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
            var (memberTypeName, unreachableMemberTypeName) = GetEmittableTypeName(
                memberTypeArg,
                knownDataSourceTypes,
                AttributeHelper.FullyQualifiedTypeFormat);

            var deferredEnumeration = attribute.NamedArguments
                .Any(arg => arg.Key == "DeferredEnumeration" && arg.Value.Value is true);

            var member = ResolveTestDataMember(memberType, memberName, knownDataSourceTypes);

            builder.Add(new TestDataSource(
                memberName,
                memberTypeName,
                member.Kind,
                member.Shape,
                member.AcceptsCancellationToken,
                deferredEnumeration,
                unreachableMemberTypeName));
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

            if (!metadataName.StartsWith(NextUnitAttributeNames.MetadataNames.ClassDataSourceAttributePrefix, StringComparison.Ordinal) ||
                constructedFrom.ContainingNamespace.ToDisplayString() != NextUnitAttributeNames.Namespace)
            {
                continue;
            }

            var sharedType = 0;
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

            foreach (var typeArg in attrClass.TypeArguments)
            {
                var typeName = typeArg.ToDisplayString(AttributeHelper.TypeExpressionFormat);
                builder.Add(new ClassDataSource(typeName, sharedType, key));
            }
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<MatrixParameterDescriptor> GetMatrixParameters(IMethodSymbol methodSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<MatrixParameterDescriptor>();

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];

            foreach (var attribute in parameter.GetAttributes())
            {
                if (!AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.Matrix))
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
            if (!AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.MatrixExclusion))
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

    public static EquatableArray<ParameterDataSourceDescriptor> GetCombinedParameterSources(
        IMethodSymbol methodSymbol,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var builder = ImmutableArray.CreateBuilder<ParameterDataSourceDescriptor>();
        var hasAnySource = false;

        for (var i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var parameter = methodSymbol.Parameters[i];
            var descriptor = TryGetParameterDataSource(parameter, i, knownDataSourceTypes);

            if (descriptor is not null)
            {
                hasAnySource = true;
                builder.Add(descriptor);
            }
        }

        return hasAnySource
            ? new EquatableArray<ParameterDataSourceDescriptor>(builder.ToImmutable())
            : EquatableArray<ParameterDataSourceDescriptor>.Empty;
    }

    private static ParameterDataSourceDescriptor? TryGetParameterDataSource(
        IParameterSymbol parameter,
        int index,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        foreach (var attribute in parameter.GetAttributes())
        {
            if (AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.Values) &&
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

            if (AttributeHelper.IsAttribute(attribute, NextUnitAttributeNames.ValuesFromMember) &&
                attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string memberName &&
                !string.IsNullOrEmpty(memberName))
            {
                var memberTypeArg = attribute.NamedArguments
                    .Where(arg => arg.Key == "MemberType")
                    .Select(arg => arg.Value.Value as INamedTypeSymbol)
                    .FirstOrDefault(t => t is not null);

                var memberType = memberTypeArg ?? parameter.ContainingSymbol.ContainingType;
                var (memberTypeName, unreachableMemberTypeName) = GetEmittableTypeName(
                    memberTypeArg,
                    knownDataSourceTypes,
                    AttributeHelper.TypeExpressionFormat);

                return new ParameterDataSourceDescriptor(
                    parameterIndex: index,
                    parameterName: parameter.Name,
                    kind: ParameterDataSourceKind.Member,
                    inlineValues: EquatableArray<ConstantValue>.Empty,
                    memberName: memberName,
                    memberTypeName: memberTypeName,
                    memberKind: GetDataSourceMemberKind(memberType, memberName, knownDataSourceTypes),
                    classTypeName: null,
                    sharedType: 0,
                    sharedKey: null,
                    unreachableMemberTypeName: unreachableMemberTypeName);
            }

            var attrClass = attribute.AttributeClass;
            if (attrClass is { IsGenericType: true })
            {
                var constructedFrom = attrClass.ConstructedFrom;
                var metadataName = constructedFrom.MetadataName;

                if (metadataName.StartsWith(NextUnitAttributeNames.MetadataNames.ValuesFromAttributePrefix, StringComparison.Ordinal) &&
                    constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitAttributeNames.Namespace)
                {
                    var typeArg = attrClass.TypeArguments[0];
                    var classTypeName = typeArg.ToDisplayString(AttributeHelper.TypeExpressionFormat);
                    var sharedType = 0;
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
    /// Splits an explicit <c>MemberType</c> into the name the generated registry may emit and the
    /// name it may only mention in a message.
    /// </summary>
    /// <remarks>
    /// Every emitted use of this name is a <c>typeof()</c> -- the descriptor's own type reference,
    /// and the <c>DynamicDependency</c> that keeps the type from being trimmed -- and <c>typeof</c>
    /// on an unreachable type fails the consumer's build with <c>CS0122</c>. That is the failure
    /// <c>NU0020</c> exists to replace, so withholding the provider is not enough: the name has to
    /// go too.
    /// <para>
    /// It is returned separately rather than simply dropped, because the runtime reads a source that
    /// names no type as one declared on the test class. A source whose named type was withheld is
    /// not that: letting it fall through would read a same-named member of the test class and
    /// silently supply the wrong rows. The emitters give it a provider that throws instead, which
    /// carries the name as a string literal rather than as a type reference.
    /// </para>
    /// <para>
    /// Trimming loses nothing: the dropped <c>DynamicDependency</c> would have preserved reflection
    /// over a private type, which never worked under Native AOT to begin with.
    /// </para>
    /// </remarks>
    private static (string? Emittable, string? Unreachable) GetEmittableTypeName(
        INamedTypeSymbol? memberTypeArg,
        KnownDataSourceTypes knownDataSourceTypes,
        SymbolDisplayFormat format)
    {
        if (memberTypeArg is null)
        {
            return (null, null);
        }

        var name = memberTypeArg.ToDisplayString(format);

        return GeneratedRegistryAccess.CanReachType(memberTypeArg, knownDataSourceTypes.CompilingAssembly)
            ? (name, null)
            : (null, name);
    }

    /// <summary>
    /// Resolves how a data source member is accessed, ignoring the shape of what it returns.
    /// </summary>
    /// <remarks>
    /// Used by the parameter-level sources ([ValuesFromMember] and [ValuesFrom&lt;T&gt;]), which only
    /// expand synchronous collections. A member the generated registry cannot name is reported as
    /// unknown so that no direct access is emitted for it; the runtime reflection fallback, which
    /// reads non-public members, still reaches it, and the analyzers report it as NU0020. A source
    /// whose declaring type is the unreachable part is handled in
    /// <see cref="GetEmittableTypeName"/> instead, and does not reach the fallback at all.
    /// </remarks>
    private static DataSourceMemberKind GetDataSourceMemberKind(
        INamedTypeSymbol? typeSymbol,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        if (typeSymbol is null)
        {
            return DataSourceMemberKind.Unknown;
        }

        foreach (var member in DataSourceMemberResolver.GetCandidateMembers(
            typeSymbol,
            memberName,
            knownDataSourceTypes.CompilingAssembly))
        {
            if (!member.IsStatic)
            {
                continue;
            }

            // Arity is part of the test for the same reason it is in DataSourceMemberResolver: the
            // emitted call names no type argument, so a generic overload cannot be emitted.
            var kind = member switch
            {
                IMethodSymbol { Parameters.Length: 0, Arity: 0 } => DataSourceMemberKind.Method,
                IPropertySymbol => DataSourceMemberKind.Property,
                IFieldSymbol => DataSourceMemberKind.Field,
                _ => DataSourceMemberKind.Unknown
            };

            if (kind == DataSourceMemberKind.Unknown)
            {
                continue;
            }

            return GeneratedRegistryAccess.CanReachMember(member, knownDataSourceTypes.CompilingAssembly)
                ? kind
                : DataSourceMemberKind.Unknown;
        }

        return DataSourceMemberKind.Unknown;
    }

    /// <summary>
    /// Resolves how a <c>[TestData]</c> member is accessed together with the shape of its rows.
    /// </summary>
    /// <remarks>
    /// Member selection itself lives in <see cref="DataSourceMemberResolver"/> so the analyzers
    /// validate exactly the member this emits. A member the resolver refused to bind is reported as
    /// unknown, which emits no provider at all: the emitted forms are direct member access, so a
    /// member the registry cannot name, or one whose cancellation token the synchronous provider has
    /// no way to pass, would produce generated code that does not compile.
    /// </remarks>
    private static (DataSourceMemberKind Kind, DataSourceShape Shape, bool AcceptsCancellationToken) ResolveTestDataMember(
        INamedTypeSymbol? typeSymbol,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var resolved = DataSourceMemberResolver.Resolve(typeSymbol, memberName, knownDataSourceTypes);

        var kind = resolved.Issue == DataSourceBindingIssue.None
            ? resolved.Symbol switch
            {
                IMethodSymbol => DataSourceMemberKind.Method,
                IPropertySymbol => DataSourceMemberKind.Property,
                IFieldSymbol => DataSourceMemberKind.Field,
                _ => DataSourceMemberKind.Unknown
            }
            : DataSourceMemberKind.Unknown;

        return kind == DataSourceMemberKind.Unknown
            ? (DataSourceMemberKind.Unknown, DataSourceShape.Sync, false)
            : (kind, knownDataSourceTypes.Classify(resolved.MemberType).Shape, resolved.AcceptsCancellationToken);
    }
}
