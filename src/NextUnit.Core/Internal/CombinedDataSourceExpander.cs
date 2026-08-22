using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NextUnit.Shared;

namespace NextUnit.Internal;

/// <summary>
/// Expands <see cref="CombinedDataSourceDescriptor"/> instances into concrete <see cref="TestCaseDescriptor"/> instances
/// by resolving parameter data sources and computing the Cartesian product at runtime.
/// </summary>
/// <remarks>
/// <c>[Repeat]</c> multiplies that product here rather than in the generator. The other three
/// buckets expand it at compile time, by emitting one descriptor per iteration, but a combined
/// method's parameter sources have no compile-time length to repeat, so the count rides on the
/// descriptor and is applied once the sources have resolved.
/// <para>
/// Shared instances come from <see cref="SharedInstanceStore"/>, which <c>[ClassDataSource]</c>
/// shares through <see cref="ClassDataSourceExpander"/>: one data source type used through both
/// attributes under the same sharing scope is one instance, and the store disposes it at the end of
/// the session.
/// </para>
/// </remarks>
internal static class CombinedDataSourceExpander
{
    /// <summary>
    /// Expands a collection of combined data source descriptors into test case descriptors.
    /// </summary>
    /// <param name="descriptors">The combined data source descriptors to expand.</param>
    /// <param name="registryMaxTestCasesPerMethod">
    /// The cap carried by the registry these descriptors came from, or <see langword="null"/> when
    /// the caller has no registry to read it from.
    /// </param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> Expand(
        IEnumerable<CombinedDataSourceDescriptor> descriptors,
        int? registryMaxTestCasesPerMethod)
    {
        foreach (var descriptor in descriptors)
        {
            foreach (var testCase in ExpandSingle(descriptor, registryMaxTestCasesPerMethod))
            {
                yield return testCase;
            }
        }
    }

    /// <summary>
    /// Expands a single combined data source descriptor into test case descriptors.
    /// </summary>
    /// <param name="descriptor">The combined data source descriptor to expand.</param>
    /// <param name="registryMaxTestCasesPerMethod">
    /// The cap carried by the registry this descriptor came from, or <see langword="null"/> when the
    /// caller has no registry to read it from.
    /// </param>
    /// <returns>A collection of expanded test case descriptors.</returns>
    public static IEnumerable<TestCaseDescriptor> ExpandSingle(
        CombinedDataSourceDescriptor descriptor,
        int? registryMaxTestCasesPerMethod)
    {
        // Taken from the caller rather than read from GeneratedTestRegistryStore.Current, which is a
        // single last-writer-wins static: the VSTest adapter reads each source assembly's registry by
        // reflection off that assembly's own type, so in a run over two assemblies Current is
        // whichever module initializer happened to run last and one assembly's descriptors would be
        // bounded by the other assembly's cap.
        var maxTestCasesPerMethod = TestCaseExpansionLimits.ResolveFromEnvironment(registryMaxTestCasesPerMethod);

        // Resolve values for each parameter
        var parameterValues = new List<object?[]>();

        // The running product starts at the repeat factor rather than at one, so [Repeat] is charged
        // against the cap the way every other multiplying source is. That placement is what keeps the
        // per-source draw honest as well: PerSourceCap divides the cap by the running product, so a
        // [Repeat(5)] beside a lazy source bounds that source at a fifth of the cap instead of letting
        // it fill the whole cap and then multiply past it.
        var projected = TestCaseExpansionPolicy.ApplyRepeat(1L, descriptor.RepeatCount);
        var truncated = false;

        foreach (var source in descriptor.ParameterSources)
        {
            // Recomputed per source against what the product can still afford, so the total drawn
            // across a method stays near the limit instead of growing with the parameter count. A
            // lazy [ValuesFromMember] sequence long enough to exhaust the host does so while being
            // drained, so a limit checked only against the finished product never runs at all.
            var perSourceCap = PerSourceCap(projected, maxTestCasesPerMethod);

            try
            {
                var resolved = ResolveParameterValues(source, descriptor.TestClass, perSourceCap);
                parameterValues.Add(resolved.Values);

                truncated |= resolved.Truncated;
                projected = TestCaseExpansionPolicy.MultiplyClamped(projected, resolved.Values.Length);
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve values for parameter '{source.ParameterName}' in test '{descriptor.MethodName}'",
                    ex);
            }
        }

        // An empty source collapses the product to nothing, exactly as it did before the limit
        // existed, so an oversized sibling must not turn "no test cases" into a failed discovery.
        // Skipping the check here cannot hide a truncation, because emptiness is never something the
        // cap produced: PerSourceCap never returns less than one, so a source reports zero values
        // only when it genuinely has none, and zero values means zero combinations however long its
        // siblings are. Rejecting on a truncated sibling anyway was considered and dropped -- it
        // fails discovery for a method whose real expansion is, and always was, no test cases.
        // ComputeCartesianProduct short-circuits on a zero-length source, so nothing is built either.
        if (projected != 0)
        {
            EnsureWithinExpansionLimit(descriptor, projected, truncated, maxTestCasesPerMethod);
        }

        // Compute Cartesian product
        var combinations = ComputeCartesianProduct(parameterValues);

        var seed = new TestCaseSeed(descriptor);
        var testMethod = seed.ResolveTestInvoker();

        // Create test cases
        var repeatCount = descriptor.RepeatCount ?? 1;
        var index = 0;
        foreach (var combination in combinations)
        {
            var combinedId = $"{descriptor.BaseId}:Combined[{index}]";

            for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                // The suffix is emitted whenever the attribute is present, [Repeat(1)] included, which
                // is what TestCaseEmitter does for every compile-time expansion. Suppressing it for a
                // count of one would make the id depend on the count rather than on the attribute, and
                // raising [Repeat(1)] to [Repeat(2)] would then rename the first iteration's test case.
                // A method with no [Repeat] keeps the bare id it has always had.
                var suffixedId = descriptor.RepeatCount.HasValue
                    ? $"{combinedId}#{repeatIndex}"
                    : combinedId;

                yield return seed.CreateTestCase(
                    suffixedId,
                    combination,
                    index,
                    testMethod,
                    repeatIndex: descriptor.RepeatCount.HasValue ? repeatIndex : null);
            }

            index++;
        }
    }

    /// <summary>
    /// A resolved parameter source, and whether drawing it stopped at the cap rather than at its end.
    /// </summary>
    /// <remarks>
    /// Truncation is reported rather than inferred from the length, because an already-materialized
    /// array is handed back whole however long it is: a length at or past the cap means "cut off" for
    /// a lazy sequence and "complete, and this is the real count" for an array, and the two lead to
    /// different messages.
    /// </remarks>
    private readonly struct ResolvedSource(object?[] values, bool truncated)
    {
        public object?[] Values { get; } = values;

        public bool Truncated { get; } = truncated;
    }

    private static ResolvedSource ResolveParameterValues(
        ParameterDataSource source,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type testClass,
        int maxValues)
    {
        return source.Kind switch
        {
            ParameterDataSourceKind.Inline => new ResolvedSource(source.InlineValues ?? [], truncated: false),
            ParameterDataSourceKind.Member => ResolveMemberValues(source, testClass, maxValues),
            ParameterDataSourceKind.Class => ResolveClassValues(source, testClass, maxValues),
            _ => new ResolvedSource([], truncated: false)
        };
    }

    private static ResolvedSource ResolveMemberValues(
        ParameterDataSource source,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type testClass,
        int maxValues)
    {
        if (source.MemberProvider is not null)
        {
            return EnumerateToArray(source.MemberProvider(), maxValues);
        }

        var memberType = source.MemberType ?? testClass;
        var memberName = source.MemberName
            ?? throw new InvalidOperationException("MemberName is required for ValuesFromMember");

        // Which member the name means is decided by DataSourceMemberLookup, which walks the base
        // chain the way C# does. Searching kind by kind here instead -- every property, then every
        // field, then every method -- would read a base property for a name a derived method has
        // taken over once the hierarchy is in scope.
        if (DataSourceMemberLookup.TryReadStaticMember(memberType, memberName, out var value))
        {
            return EnumerateToArray(value, maxValues);
        }

        throw new InvalidOperationException(
            $"Member '{memberName}' not found on type '{memberType.FullName}'. " +
            "The member must be a static property, field, or parameterless method.");
    }

    private static ResolvedSource ResolveClassValues(ParameterDataSource source, Type testClass, int maxValues)
    {
        if (source.ClassDataSourceType is null)
        {
            throw new InvalidOperationException("ClassDataSourceType is required for ValuesFrom");
        }

        var instance = SharedInstanceStore.GetOrCreate(
            source.ClassDataSourceType,
            source.SharedType,
            source.SharedKey,
            testClass,
            source.ClassDataSourceFactory);

        return EnumerateToArray(instance, maxValues);
    }

    /// <summary>
    /// Materializes a resolved source into an array, drawing no more than <paramref name="maxValues"/>
    /// values from a lazy sequence.
    /// </summary>
    /// <remarks>
    /// An already-materialized array is handed back whole: the allocation the cap exists to prevent
    /// has already been made by the caller's own code, and copying a prefix of it would only add a
    /// second one. Its length still reaches the expansion check, so an oversized array is rejected
    /// there -- with its real count, since nothing was cut off.
    /// </remarks>
    private static ResolvedSource EnumerateToArray(object? value, int maxValues)
    {
        if (value is null)
        {
            return new ResolvedSource([], truncated: false);
        }

        if (value is object?[] array)
        {
            return new ResolvedSource(array, truncated: false);
        }

        if (value is IEnumerable enumerable)
        {
            // One past the cap, so filling the cap is distinguishable from ending exactly at it.
            // PerSourceCap keeps maxValues below int.MaxValue precisely so this addition cannot
            // wrap: a negative count makes Take yield nothing, which would turn an oversized source
            // into a silently empty one.
            var drawn = enumerable.Cast<object?>().Take(maxValues + 1).ToArray();

            return drawn.Length > maxValues
                ? new ResolvedSource(drawn[..maxValues], truncated: true)
                : new ResolvedSource(drawn, truncated: false);
        }

        return new ResolvedSource([value], truncated: false);
    }

    /// <summary>
    /// Rejects an expansion that would exceed the configured cap, before anything is materialized.
    /// </summary>
    /// <remarks>
    /// The check runs on the resolved source lengths rather than on the product, because the product
    /// is the thing that must not be built: <see cref="ComputeCartesianProduct"/> allocates one array
    /// per combination and holds them all, so a check placed after it never runs.
    /// </remarks>
    private static void EnsureWithinExpansionLimit(
        CombinedDataSourceDescriptor descriptor,
        long projected,
        bool truncated,
        int maxTestCasesPerMethod)
    {
        // A truncated source rejects the expansion even when the product it produced looks admissible.
        // PerSourceCap only ever cuts a source off at a length that already carries the product past
        // the limit, so this is unreachable unless the cap had to be clamped -- and expanding a
        // prefix would report a green run over a suite that was silently shortened.
        if (projected <= maxTestCasesPerMethod && !truncated)
        {
            return;
        }

        var countText = truncated || projected == long.MaxValue
            ? $"more than {maxTestCasesPerMethod}"
            : projected.ToString(CultureInfo.InvariantCulture);

        throw new InvalidOperationException(
            $"Test '{descriptor.MethodName}' expands to {countText} test cases, which exceeds the limit of " +
            $"{maxTestCasesPerMethod}. Reduce the parameter data sources, raise the limit for the " +
            $"project with <NextUnitMaxTestCasesPerMethod>, or raise it for this run only with the " +
            $"{TestCaseExpansionLimits.EnvironmentVariableName} environment variable.");
    }

    /// <summary>
    /// How many values the next source may contribute before the product is certainly over the limit.
    /// </summary>
    /// <remarks>
    /// One more than the running product can still afford, so a source that stays within it cannot
    /// have been cut off, and a source that reaches it has already carried the product past the
    /// limit. That is what makes a truncated prefix unable to reach
    /// <see cref="ComputeCartesianProduct"/>: every truncation implies a rejection.
    /// <para>
    /// Once the product is settled -- already over the limit, or already collapsed to zero by an
    /// empty source -- one value is all a remaining source can still contribute to the decision, but
    /// it is still resolved, so sharing a <c>[ValuesFrom]</c> instance keeps happening where it did.
    /// </para>
    /// <para>
    /// The result stays below <see cref="int.MaxValue"/> so that drawing one past it is always
    /// expressible, which is what keeps truncation detectable at every configured limit. A limit set
    /// that high therefore behaves as <see cref="int.MaxValue"/> minus one; drawing billions of values
    /// exhausts the host either way, and that is what such a setting asks for.
    /// </para>
    /// </remarks>
    private static int PerSourceCap(long projected, int maxTestCasesPerMethod)
    {
        if (projected <= 0 || projected > maxTestCasesPerMethod)
        {
            return 1;
        }

        var affordable = maxTestCasesPerMethod / projected;

        return affordable >= int.MaxValue - 1 ? int.MaxValue - 1 : (int)affordable + 1;
    }

    private static List<object?[]> ComputeCartesianProduct(List<object?[]> parameterValues)
    {
        if (parameterValues.Count == 0)
        {
            return [];
        }

        var result = new List<object?[]>();
        var indices = new int[parameterValues.Count];
        var lengths = parameterValues.Select(v => v.Length).ToArray();

        // Check for empty parameter values
        if (lengths.Any(l => l == 0))
        {
            return [];
        }

        while (true)
        {
            // Build current combination
            var combination = new object?[parameterValues.Count];
            for (var i = 0; i < parameterValues.Count; i++)
            {
                combination[i] = parameterValues[i][indices[i]];
            }
            result.Add(combination);

            // Increment indices (like a multi-digit counter)
            var carry = true;
            for (var i = indices.Length - 1; i >= 0 && carry; i--)
            {
                indices[i]++;
                if (indices[i] >= lengths[i])
                {
                    indices[i] = 0;
                }
                else
                {
                    carry = false;
                }
            }

            // If we carried out of the first position, we're done
            if (carry)
            {
                break;
            }
        }

        return result;
    }

}
