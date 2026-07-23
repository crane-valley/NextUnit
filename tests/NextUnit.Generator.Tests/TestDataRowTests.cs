using NextUnit.Internal;

namespace NextUnit.Generator.Tests;

public sealed class TestDataRowTests
{
    [Fact]
    public void TestDataExpander_TypedTupleRow_PreservesArgumentsAndMetadata()
    {
        var descriptor = CreateTestDataDescriptor();

        var testCase = Assert.Single(TestDataExpander.ExpandSingle(descriptor));

        Assert.Equal("adds positive values", testCase.DisplayName);
        Assert.Equal(new object?[] { 2, 3, 5 }, testCase.Arguments);
        Assert.Equal(new[] { "Method", "Row" }, testCase.Categories);
        Assert.Equal(new[] { "Fast", "Smoke" }, testCase.Tags);
        Assert.True(testCase.IsSkipped);
        Assert.Equal("Tracked issue", testCase.SkipReason);
        Assert.Equal(
            $"{descriptor.BaseId}:{typeof(TestDataRowTests).FullName}.{nameof(GetRows)}[0]",
            testCase.Id.Value);
    }

    [Fact]
    public async Task TestDataExpander_ManualDescriptor_CreatesReflectionFallbackAsync()
    {
        var testCase = Assert.Single(TestDataExpander.ExpandSingle(CreateTestDataDescriptor()));

        Xunit.Assert.NotNull(testCase.TestMethodWithArguments);
        var invoker = testCase.TestMethodWithArguments!;
        await invoker(
            new Target(),
            testCase.Arguments!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ClassDataSourceExpander_TypedTupleRow_PreservesArgumentsAndMetadata()
    {
        var descriptor = new ClassDataSourceDescriptor
        {
            BaseId = "Tests.Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceTypes = [typeof(RowSource)],
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            Categories = ["Method"],
            Tags = ["Fast"]
        };

        var testCase = Assert.Single(ClassDataSourceExpander.ExpandSingle(descriptor));

        Assert.Equal("class row", testCase.DisplayName);
        Assert.Equal(new object?[] { 4, 5, 9 }, testCase.Arguments);
        Assert.Equal(new[] { "Method", "ClassSource" }, testCase.Categories);
        Assert.Equal(new[] { "Fast", "Database" }, testCase.Tags);
        Assert.False(testCase.IsSkipped);
        Assert.Null(testCase.SkipReason);
        Assert.Equal("Tests.Add:ClassData:RowSource[0]", testCase.Id.Value);
    }

    [Fact]
    public void TestDataRow_InvalidMetadata_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new TestDataRow<int>(1, categories: ["Valid", " "]));
        Assert.Throws<ArgumentException>(
            () => new TestDataRow<int>(1, tags: ["Valid", ""]));
        Assert.Throws<ArgumentException>(
            () => new TestDataRow<int>(1, skipReason: " "));
    }

    [Fact]
    public void TestDataRow_ScalarEnumerableAndNull_RemainSingleArguments()
    {
        var values = new List<int> { 1, 2 };

        var enumerableRow = TestDataRowResolver.Resolve(new TestDataRow<List<int>>(values));
        var nullRow = TestDataRowResolver.Resolve(new TestDataRow<string?>(null));

        Assert.Equal(new object?[] { values }, enumerableRow.Arguments);
        Assert.Equal(new object?[] { null }, nullRow.Arguments);
    }

    public static IEnumerable<TestDataRow<(int A, int B, int Expected)>> GetRows()
    {
        yield return new TestDataRow<(int A, int B, int Expected)>(
            (2, 3, 5),
            displayName: "adds positive values",
            categories: ["Row", "Method"],
            tags: ["Smoke", "Fast"],
            skipReason: "Tracked issue");
    }

    private static TestDataDescriptor CreateTestDataDescriptor() => new()
    {
        BaseId = "Tests.Add",
        TestClass = typeof(Target),
        MethodName = nameof(Target.Add),
        DataSourceName = nameof(GetRows),
        DataSourceType = typeof(TestDataRowTests),
        ParameterTypes = [typeof(int), typeof(int), typeof(int)],
        Categories = ["Method"],
        Tags = ["Fast"]
    };

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }
    }

    private sealed class RowSource : IEnumerable<TestDataRow<(int A, int B, int Expected)>>
    {
        public IEnumerator<TestDataRow<(int A, int B, int Expected)>> GetEnumerator()
        {
            yield return new TestDataRow<(int A, int B, int Expected)>(
                (4, 5, 9),
                displayName: "class row",
                categories: ["ClassSource"],
                tags: ["Database"]);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
