using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how <c>[Repeat]</c> multiplies a <c>[TestData]</c> member and a <c>[ClassDataSource]</c>
/// type.
/// </summary>
/// <remarks>
/// Both buckets are emitted as one descriptor per source rather than as one test case per iteration,
/// so the count has to survive as descriptor state and be applied where the rows appear. Both halves
/// are covered here -- what the generator writes, and what each expander does with it -- because a
/// count emitted but never applied is exactly the silent drop this replaced.
/// </remarks>
public class MemberSourceRepeatExpansionTests
{
    private const string BaseId = "Tests.Repeat";

    private static readonly int _limit = TestCaseExpansionLimits.ResolveFromEnvironment(registryBaseline: null);

    private static readonly string _rowIdPrefix =
        $"{BaseId}:{typeof(MemberSourceRepeatExpansionTests).FullName}.Rows";

    private static readonly string _classRowIdPrefix = $"{BaseId}:ClassData:{nameof(RowSource)}";

    [Fact]
    public async Task TestDataSourceWithRepeat_EmitsTheCountAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class DataTests
            {
                public static IEnumerable<object[]> Rows()
                {
                    yield return new object[] { 1, 2 };
                }

                [Test]
                [Repeat(5)]
                [TestData(nameof(Rows))]
                public void Run(int a, int b)
                {
                }
            }
            """);

        // The descriptor is the only place the count can survive to: the emitter writes no test case
        // for a [TestData] method, so there is nothing here for it to have been folded into.
        Assert.Contains("RepeatCount = 5,", registry);
    }

    [Fact]
    public async Task ClassDataSourceWithRepeat_EmitsTheCountAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using System.Collections;
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator()
                {
                    yield return new object[] { 1, 2 };
                }

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class DataTests
            {
                [Test]
                [Repeat(4)]
                [ClassDataSource<Rows>]
                public void Run(int a, int b)
                {
                }
            }
            """);

        Assert.Contains("RepeatCount = 4,", registry);
    }

    [Fact]
    public async Task MemberSourcesWithoutRepeat_EmitNoCountAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using System.Collections;
            using System.Collections.Generic;
            using NextUnit;

            namespace TestProject;

            public class Rows : IEnumerable<object[]>
            {
                public IEnumerator<object[]> GetEnumerator()
                {
                    yield return new object[] { 1, 2 };
                }

                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class DataTests
            {
                public static IEnumerable<object[]> MemberRows()
                {
                    yield return new object[] { 1, 2 };
                }

                [Test]
                [TestData(nameof(MemberRows))]
                public void FromMember(int a, int b)
                {
                }

                [Test]
                [ClassDataSource<Rows>]
                public void FromClass(int a, int b)
                {
                }
            }
            """);

        // Emitting a null count for every runtime descriptor would churn every existing baseline for
        // a property that already defaults to null.
        Assert.False(
            registry.Contains("RepeatCount", StringComparison.Ordinal),
            "A member source without [Repeat] must not emit a repeat count.");
    }

    [Fact]
    public void TestData_WithRepeat_MultipliesTheRowsAndSuffixesTheIds()
    {
        var testCases = TestDataExpander
            .ExpandSingle(CreateTestDataDescriptor(repeatCount: 2), CancellationToken.None)
            .ToList();

        Assert.Equal(
            new[]
            {
                $"{_rowIdPrefix}[0]#0",
                $"{_rowIdPrefix}[0]#1",
                $"{_rowIdPrefix}[1]#0",
                $"{_rowIdPrefix}[1]#1",
            },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.Equal([0, 1, 0, 1], testCases.Select(testCase => testCase.RepeatIndex));
        Assert.All(testCases, testCase => Assert.EndsWith(
            $" (Repeat #{(testCase.RepeatIndex!.Value + 1).ToString(CultureInfo.InvariantCulture)})",
            testCase.DisplayName));

        // Every iteration runs the same row, so the arguments must not shift with the suffix.
        Assert.Equal(new object?[] { 1, 2 }, testCases[1].Arguments);
    }

    [Fact]
    public void TestData_WithRepeatOfOne_StillSuffixesTheId()
    {
        // The suffix tracks the attribute, not the count. Suppressing it at one would rename the
        // first iteration of a test case the moment [Repeat(1)] became [Repeat(2)].
        var testCases = TestDataExpander
            .ExpandSingle(CreateTestDataDescriptor(repeatCount: 1), CancellationToken.None)
            .ToList();

        Assert.Equal(
            new[] { $"{_rowIdPrefix}[0]#0", $"{_rowIdPrefix}[1]#0" },
            testCases.Select(testCase => testCase.Id.Value));
    }

    [Fact]
    public void TestData_WithoutRepeat_KeepsTheBareIds()
    {
        var testCases = TestDataExpander
            .ExpandSingle(CreateTestDataDescriptor(repeatCount: null), CancellationToken.None)
            .ToList();

        // Threading the count through must not move an id that no [Repeat] participates in; these are
        // published test case ids that a rerun, a filter, or a CI history refers to by name.
        Assert.Equal(
            new[] { $"{_rowIdPrefix}[0]", $"{_rowIdPrefix}[1]" },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.All(testCases, testCase => Assert.Null(testCase.RepeatIndex));
    }

    [Fact]
    public async Task TestData_AsyncSourceWithRepeat_MultipliesTheRowsAsync()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = BaseId,
            DisplayName = "Run",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Run),
            DataSourceName = nameof(StreamRowsAsync),
            DataSourceType = typeof(MemberSourceRepeatExpansionTests),
            ParameterTypes = [typeof(int), typeof(int)],
            RepeatCount = 2,
            AsyncDataSourceProvider = static ct =>
                AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct)
        };

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        Assert.Equal(4, testCases.Count);
        Assert.Equal(
            $"{BaseId}:{typeof(MemberSourceRepeatExpansionTests).FullName}.{nameof(StreamRowsAsync)}[1]#1",
            testCases[3].Id.Value);
    }

    [Fact]
    public void ClassDataSource_WithRepeat_MultipliesTheRowsAndSuffixesTheIds()
    {
        var testCases = ClassDataSourceExpander
            .ExpandSingle(CreateClassDataSourceDescriptor(repeatCount: 2))
            .ToList();

        Assert.Equal(
            new[]
            {
                $"{_classRowIdPrefix}[0]#0",
                $"{_classRowIdPrefix}[0]#1",
                $"{_classRowIdPrefix}[1]#0",
                $"{_classRowIdPrefix}[1]#1",
            },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.Equal([0, 1, 0, 1], testCases.Select(testCase => testCase.RepeatIndex));
        Assert.All(testCases, testCase => Assert.EndsWith(
            $" (Repeat #{(testCase.RepeatIndex!.Value + 1).ToString(CultureInfo.InvariantCulture)})",
            testCase.DisplayName));
    }

    [Fact]
    public void ClassDataSource_WithoutRepeat_KeepsTheBareIds()
    {
        var testCases = ClassDataSourceExpander
            .ExpandSingle(CreateClassDataSourceDescriptor(repeatCount: null))
            .ToList();

        Assert.Equal(
            new[] { $"{_classRowIdPrefix}[0]", $"{_classRowIdPrefix}[1]" },
            testCases.Select(testCase => testCase.Id.Value));

        Assert.All(testCases, testCase => Assert.Null(testCase.RepeatIndex));
    }

    /// <summary>
    /// A deferred source stays one placeholder however large the repeat count is. Multiplying at
    /// discovery would report a row count that discovery is not allowed to know.
    /// </summary>
    [Fact]
    public async Task DeferredTestData_WithRepeat_ReportsOneUnsuffixedPlaceholderAsync()
    {
        var invocations = 0;
        var descriptor = CreateTestDataDescriptor(repeatCount: 3, deferred: true, () => invocations++);

        var testCases = await TestDataExpander.ExpandAsync(
            [descriptor],
            TestContext.Current.CancellationToken);

        var placeholder = Assert.Single(testCases);
        Assert.Equal(0, invocations);
        Assert.Equal(_rowIdPrefix, placeholder.Id.Value);
        Assert.Null(placeholder.RepeatIndex);
        Assert.Same(descriptor, placeholder.DeferredDataSource);
    }

    /// <summary>
    /// The repeat rides on the rows the placeholder expands into, so a deferred row is addressable
    /// exactly like an eager one.
    /// </summary>
    [Fact]
    public async Task ExpandDeferredAsync_WithRepeat_MultipliesTheRowsAsync()
    {
        var testCases = await TestDataExpander.ExpandDeferredAsync(
            CreateTestDataDescriptor(repeatCount: 3, deferred: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(6, testCases.Count);
        Assert.Equal($"{_rowIdPrefix}[0]#0", testCases[0].Id.Value);
        Assert.Equal($"{_rowIdPrefix}[1]#2", testCases[5].Id.Value);
        Assert.Equal([0, 1, 2, 0, 1, 2], testCases.Select(testCase => testCase.RepeatIndex));
    }

    [Fact]
    public async Task ExpandDeferredAsync_AsyncSourceWithRepeat_MultipliesTheRowsAsync()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = BaseId,
            DisplayName = "Run",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Run),
            DataSourceName = nameof(StreamRowsAsync),
            DataSourceType = typeof(MemberSourceRepeatExpansionTests),
            ParameterTypes = [typeof(int), typeof(int)],
            DeferredEnumeration = true,
            RepeatCount = 2,
            AsyncDataSourceProvider = static ct =>
                AsyncDataSourceAdapter.FromAsyncEnumerableAsync(StreamRowsAsync(ct), ct)
        };

        var testCases = await TestDataExpander.ExpandDeferredAsync(
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.Equal(4, testCases.Count);
        Assert.Equal([0, 1, 0, 1], testCases.Select(testCase => testCase.RepeatIndex));
    }

    /// <summary>
    /// The rows of these two sources are the user's own code and stay uncapped, so a source longer
    /// than the cap must still expand.
    /// </summary>
    [Fact]
    public void TestData_RowsOverTheCap_AreNotRejected()
    {
        var descriptor = new TestDataDescriptor
        {
            BaseId = BaseId,
            DisplayName = "Run",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Run),
            DataSourceName = "ManyRows",
            DataSourceType = typeof(MemberSourceRepeatExpansionTests),
            ParameterTypes = [typeof(int), typeof(int)],
            DataSourceProvider = () => ManyRows(_limit + 1)
        };

        var testCases = TestDataExpander.ExpandSingle(descriptor, CancellationToken.None).ToList();

        Assert.Equal(_limit + 1, testCases.Count);
    }

    [Fact]
    public void TestData_RepeatOverTheCap_IsRejected()
    {
        // The rows are two; the repeat is what carries the method over the cap, and nothing else
        // charges it -- TestCaseExpansionValidator charges this bucket one descriptor.
        var descriptor = CreateTestDataDescriptor(repeatCount: _limit + 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, CancellationToken.None).ToList());

        Assert.Contains((_limit + 1).ToString(CultureInfo.InvariantCulture), exception.Message);
        Assert.Contains(_limit.ToString(CultureInfo.InvariantCulture), exception.Message);
    }

    /// <summary>
    /// Refused before the member runs, which is what makes the refusal reachable for a deferred
    /// source at all: reading its rows is the one thing discovery must not do.
    /// </summary>
    [Fact]
    public async Task DeferredTestData_RepeatOverTheCap_IsRejectedWithoutReadingTheSourceAsync()
    {
        var invocations = 0;
        var descriptor = CreateTestDataDescriptor(repeatCount: _limit + 1, deferred: true, () => invocations++);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await TestDataExpander.ExpandAsync(
                [descriptor],
                TestContext.Current.CancellationToken));

        Assert.Equal(0, invocations);
    }

    /// <summary>
    /// A count below one cannot come from the attribute, so it means a hand-written registry.
    /// Refused rather than clamped to one: running the test once would report a green suite over a
    /// registry that asked for something impossible, and expanding to nothing hides it just as well.
    /// </summary>
    [Fact]
    public void TestData_NonPositiveRepeat_IsRejected()
    {
        var descriptor = CreateTestDataDescriptor(repeatCount: 0);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestDataExpander.ExpandSingle(descriptor, CancellationToken.None).ToList());

        Assert.Contains("not positive", exception.Message);
    }

    [Fact]
    public void ClassDataSource_NonPositiveRepeat_IsRejected()
    {
        var descriptor = CreateClassDataSourceDescriptor(repeatCount: -1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClassDataSourceExpander.ExpandSingle(descriptor).ToList());

        Assert.Contains("not positive", exception.Message);
    }

    [Fact]
    public void ClassDataSource_RepeatOverTheCap_IsRejected()
    {
        var descriptor = CreateClassDataSourceDescriptor(repeatCount: _limit + 1);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ClassDataSourceExpander.ExpandSingle(descriptor).ToList());

        Assert.Contains(_limit.ToString(CultureInfo.InvariantCulture), exception.Message);
    }

    /// <summary>
    /// The cap the registry carries is the baseline, so a project that raised it admits a count the
    /// built-in default refuses.
    /// </summary>
    [Fact]
    public void TestData_RepeatUnderTheRegistryCap_IsAdmitted()
    {
        var descriptor = CreateTestDataDescriptor(repeatCount: _limit + 1);

        var testCases = TestDataExpander
            .ExpandSingle(descriptor, CancellationToken.None, registryMaxTestCasesPerMethod: _limit + 1)
            .ToList();

        Assert.Equal(2 * (_limit + 1), testCases.Count);
    }

    private static TestDataDescriptor CreateTestDataDescriptor(
        int? repeatCount,
        bool deferred = false,
        Action? onInvoke = null) =>
        new()
        {
            BaseId = BaseId,
            DisplayName = "Run",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Run),
            DataSourceName = "Rows",
            DataSourceType = typeof(MemberSourceRepeatExpansionTests),
            ParameterTypes = [typeof(int), typeof(int)],
            DeferredEnumeration = deferred,
            RepeatCount = repeatCount,
            DataSourceProvider = () =>
            {
                onInvoke?.Invoke();
                return Rows();
            }
        };

    private static ClassDataSourceDescriptor CreateClassDataSourceDescriptor(int? repeatCount) =>
        new()
        {
            BaseId = BaseId,
            DisplayName = "Run",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Run),
            ParameterTypes = [typeof(int), typeof(int)],
            DataSourceTypes = [typeof(RowSource)],
            DataSourceFactories = [static () => new RowSource()],
            RepeatCount = repeatCount
        };

    private static IEnumerable<object[]> Rows()
    {
        yield return [1, 2];
        yield return [3, 4];
    }

    private static IEnumerable<object[]> ManyRows(int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return [index, index];
        }
    }

    private static async IAsyncEnumerable<object[]> StreamRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return [1, 2];
        yield return [3, 4];
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }

    private sealed class RowSource : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Rows().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Target
    {
        public void Run(int a, int b) => _ = a + b;
    }
}
