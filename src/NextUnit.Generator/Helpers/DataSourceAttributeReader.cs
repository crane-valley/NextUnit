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
            string? memberTypeName = memberTypeArg?.ToDisplayString(AttributeHelper.FullyQualifiedTypeFormat);

            var member = ResolveDataSourceMember(memberType, memberName, knownDataSourceTypes);

            builder.Add(new TestDataSource(
                memberName,
                memberTypeName,
                member.Kind,
                member.Shape,
                member.AcceptsCancellationToken));
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
                var typeName = typeArg.ToDisplayString(AttributeHelper.TypeofCompatibleFormat);
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

        return hasAnySource
            ? new EquatableArray<ParameterDataSourceDescriptor>(builder.ToImmutable())
            : EquatableArray<ParameterDataSourceDescriptor>.Empty;
    }

    private static ParameterDataSourceDescriptor? TryGetParameterDataSource(IParameterSymbol parameter, int index)
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
                string? memberTypeName = memberTypeArg?.ToDisplayString(AttributeHelper.TypeofCompatibleFormat);

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

            var attrClass = attribute.AttributeClass;
            if (attrClass is { IsGenericType: true })
            {
                var constructedFrom = attrClass.ConstructedFrom;
                var metadataName = constructedFrom.MetadataName;

                if (metadataName.StartsWith(NextUnitAttributeNames.MetadataNames.ValuesFromAttributePrefix, StringComparison.Ordinal) &&
                    constructedFrom.ContainingNamespace.ToDisplayString() == NextUnitAttributeNames.Namespace)
                {
                    var typeArg = attrClass.TypeArguments[0];
                    var classTypeName = typeArg.ToDisplayString(AttributeHelper.TypeofCompatibleFormat);
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
    /// Resolves how a data source member is accessed, ignoring the shape of what it returns.
    /// </summary>
    /// <remarks>
    /// Used by the parameter-level sources ([ValuesFromMember] and [ValuesFrom&lt;T&gt;]), which only
    /// expand synchronous collections.
    /// </remarks>
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

    /// <summary>
    /// Resolves how a <c>[TestData]</c> member is accessed together with the shape of its rows.
    /// </summary>
    /// <remarks>
    /// Member selection itself lives in <see cref="DataSourceMemberResolver"/> so the analyzers
    /// validate exactly the member this emits.
    /// </remarks>
    private static (DataSourceMemberKind Kind, DataSourceShape Shape, bool AcceptsCancellationToken) ResolveDataSourceMember(
        INamedTypeSymbol? typeSymbol,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var resolved = DataSourceMemberResolver.Resolve(typeSymbol, memberName, knownDataSourceTypes);

        var kind = resolved.Symbol switch
        {
            IMethodSymbol => DataSourceMemberKind.Method,
            IPropertySymbol => DataSourceMemberKind.Property,
            IFieldSymbol => DataSourceMemberKind.Field,
            _ => DataSourceMemberKind.Unknown
        };

        return kind == DataSourceMemberKind.Unknown
            ? (DataSourceMemberKind.Unknown, DataSourceShape.Sync, false)
            : (kind, knownDataSourceTypes.Classify(resolved.MemberType).Shape, resolved.AcceptsCancellationToken);
    }
}
