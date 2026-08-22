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
                memberName: memberName,
                memberTypeName: memberTypeName,
                declaringTypeName: member.DeclaringTypeName,
                memberKind: member.Kind,
                shape: member.Shape,
                rowTypeName: member.RowTypeName,
                acceptsCancellationToken: member.AcceptsCancellationToken,
                deferredEnumeration: deferredEnumeration,
                unreachableMemberTypeName: unreachableMemberTypeName));
        }

        return builder.ToImmutable();
    }

    public static EquatableArray<ClassDataSource> GetClassDataSources(
        IMethodSymbol methodSymbol,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        var builder = ImmutableArray.CreateBuilder<ClassDataSource>();

        foreach (var attribute in methodSymbol.GetAttributes())
        {
            var sourceTypes = ClassDataSourceAttributeMatcher.GetClassDataSourceTypes(attribute);
            if (sourceTypes.IsEmpty)
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

            foreach (var typeArg in sourceTypes)
            {
                var typeName = typeArg.ToDisplayString(AttributeHelper.TypeExpressionFormat);

                // Named only for a source offering more than one row type, exactly as a [TestData]
                // member is: the runtime reads the instance as a non-generic IEnumerable, so a
                // second arm makes the rows a property of the source type's interface map rather
                // than of what NU0009 validated. A class data source compiles today, so a name the
                // generated file cannot write is given up on rather than emitted.
                var classification = knownDataSourceTypes.Classify(typeArg);
                var rowTypeName = classification.RowTypeIsAmbiguous
                    ? GetRowTypeName(classification.RowType, requireWritableName: true, knownDataSourceTypes)
                    : null;

                builder.Add(new ClassDataSource(typeName, sharedType, key, rowTypeName));
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
        var selected = ParameterDataSourceSelector.Select(parameter);
        if (selected.Attribute is not { } attribute)
        {
            return null;
        }

        switch (selected.Kind)
        {
            case ParameterDataSourceAttributeKind.Values:
                return new ParameterDataSourceDescriptor(
                    parameterIndex: index,
                    parameterName: parameter.Name,
                    kind: ParameterDataSourceKind.Inline,
                    inlineValues: ConstantValueFactory.CreateRange(attribute.ConstructorArguments[0].Values),
                    memberName: null,
                    memberTypeName: null,
                    declaringTypeName: null,
                    memberKind: DataSourceMemberKind.Unknown,
                    classTypeName: null,
                    sharedType: 0,
                    sharedKey: null);

            case ParameterDataSourceAttributeKind.ValuesFromMember
                when attribute.ConstructorArguments[0].Value is string memberName:
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
                var (memberKind, declaringTypeName) = GetDataSourceMemberKind(
                    memberType,
                    memberName,
                    knownDataSourceTypes);

                return new ParameterDataSourceDescriptor(
                    parameterIndex: index,
                    parameterName: parameter.Name,
                    kind: ParameterDataSourceKind.Member,
                    inlineValues: EquatableArray<ConstantValue>.Empty,
                    memberName: memberName,
                    memberTypeName: memberTypeName,
                    declaringTypeName: declaringTypeName,
                    memberKind: memberKind,
                    classTypeName: null,
                    sharedType: 0,
                    sharedKey: null,
                    unreachableMemberTypeName: unreachableMemberTypeName);
            }

            case ParameterDataSourceAttributeKind.ValuesFrom
                when ClassDataSourceAttributeMatcher.GetValuesFromType(attribute) is { } typeArg:
            {
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
                    declaringTypeName: null,
                    memberKind: DataSourceMemberKind.Unknown,
                    classTypeName: classTypeName,
                    sharedType: sharedType,
                    sharedKey: key);
            }

            default:
                return null;
        }
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
    private static (DataSourceMemberKind Kind, string? DeclaringTypeName) GetDataSourceMemberKind(
        INamedTypeSymbol? typeSymbol,
        string memberName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        if (typeSymbol is null)
        {
            return (DataSourceMemberKind.Unknown, null);
        }

        foreach (var member in DataSourceMemberResolver.GetCandidateMembers(typeSymbol, memberName))
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
                ? (kind, GetDeclaringTypeName(member, knownDataSourceTypes))
                : (DataSourceMemberKind.Unknown, null);
        }

        return (DataSourceMemberKind.Unknown, null);
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
    private static (DataSourceMemberKind Kind, string? DeclaringTypeName, DataSourceShape Shape, string? RowTypeName, bool AcceptsCancellationToken) ResolveTestDataMember(
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

        if (kind == DataSourceMemberKind.Unknown)
        {
            return (DataSourceMemberKind.Unknown, null, DataSourceShape.Sync, null, false);
        }

        // Classified once and read twice: the walk covers every interface the member's type
        // implements, and the row type has to be the one this shape was decided from.
        var classification = knownDataSourceTypes.Classify(resolved.MemberType);

        // Named only when the source offers more than one, which is the only case inference answers
        // differently -- or not at all. Naming an unambiguous row type would change nothing except
        // to add a way for the emitted call to fail on a type inference resolves without a name.
        var rowTypeName = classification.RowTypeIsAmbiguous
            ? GetRowTypeName(classification, knownDataSourceTypes)
            : null;

        return (
            kind,
            GetDeclaringTypeName(resolved.Symbol, knownDataSourceTypes),
            classification.Shape,
            rowTypeName,
            resolved.AcceptsCancellationToken);
    }

    /// <summary>
    /// Names the selected row type for the emitted adapter call, or <c>null</c> to emit no name.
    /// </summary>
    /// <remarks>
    /// Every shape but <see cref="DataSourceShape.AsyncEnumerable"/> has to prove the generated file
    /// can write the name first, and gives it up when it cannot: those sources compile today and
    /// only read the wrong arm, so a name the file cannot resolve -- an <c>extern alias</c> hiding
    /// the assembly is the case that shows up first -- or one that spells a type retired as an error
    /// would trade a wrong row for a build the user cannot fix, and the wrong row is the lesser of
    /// the two. An <c>IAsyncEnumerable</c> source is not asked, because without the name it does not
    /// compile at all: inference reports <c>CS0411</c> against the generated file, so there is no
    /// working build for an unwritable name to cost.
    /// </remarks>
    private static string? GetRowTypeName(
        DataSourceClassification classification,
        KnownDataSourceTypes knownDataSourceTypes) =>
        GetRowTypeName(
            classification.RowType,
            requireWritableName: classification.Shape != DataSourceShape.AsyncEnumerable,
            knownDataSourceTypes);

    private static string? GetRowTypeName(
        ITypeSymbol? rowType,
        bool requireWritableName,
        KnownDataSourceTypes knownDataSourceTypes)
    {
        if (rowType is null)
        {
            return null;
        }

        var typeExpression = rowType.ToDisplayString(AttributeHelper.TypeExpressionFormat);

        if (!requireWritableName)
        {
            return typeExpression;
        }

        // The obsolete check is separate from binding on purpose: a speculative model returns no
        // diagnostics for the expression that reports CS0619 once bound in a real tree, and CS0619
        // is an error the file's blanket pragma cannot suppress.
        return GeneratedRegistryAccess.CanReachType(rowType, knownDataSourceTypes.CompilingAssembly) &&
            GeneratedRegistryAccess.NameBindsToType(typeExpression, rowType, knownDataSourceTypes.SemanticModel) &&
            !GeneratedRegistryAccess.NameSpellsAnErrorObsoleteType(rowType)
            ? typeExpression
            : null;
    }

    /// <summary>
    /// Names the type that declares a resolved data source member, for the emitted access to be
    /// qualified with, or <c>null</c> to leave the caller on the qualifier it already had.
    /// </summary>
    /// <remarks>
    /// A type expression format rather than the one <c>MemberType</c> is read with: a member access
    /// qualifier is parsed as C#, where a nullable reference annotation would not compile. The
    /// containing type of a declaration carries none, so the two agree on every real symbol; the
    /// format is chosen for what the text has to be rather than for what it happens to produce.
    /// <para>
    /// A name that would not bind to the declaring type from the generated file is given up on
    /// instead -- an <c>extern alias</c> hiding the assembly is the case that shows up first, and
    /// the binder is asked rather than the cases enumerated. The caller then keeps the type the
    /// attribute points at, a qualifier already known to bind because the user's own source names
    /// it. That leaves the inherited-source capture this qualification closes open for such a base,
    /// which is where it was before: a name that does not compile closes nothing.
    /// </para>
    /// <para>
    /// A name that binds is given up on for one further reason: it spells the declaring type's own
    /// type arguments, which the user's source may never spell, and an <c>[Obsolete(error: true)]</c>
    /// one among them is a <c>CS0619</c> no pragma can suppress. The same fallback covers it, for the
    /// same reason.
    /// </para>
    /// </remarks>
    private static string? GetDeclaringTypeName(ISymbol? member, KnownDataSourceTypes knownDataSourceTypes)
    {
        if (member?.ContainingType is not { } declaringType)
        {
            return null;
        }

        var typeExpression = declaringType.ToDisplayString(AttributeHelper.TypeExpressionFormat);

        return GeneratedRegistryAccess.NameBindsToType(typeExpression, declaringType, knownDataSourceTypes.SemanticModel) &&
            !GeneratedRegistryAccess.NameSpellsAnErrorObsoleteType(declaringType)
            ? typeExpression
            : null;
    }
}
