using NextUnit.Internal;
using Xunit;

namespace NextUnit.TestAdapter.Tests;

public sealed class VSTestCaseFactoryTests
{
    [Fact]
    public void Create_TypedRow_PreservesIdentityDisplayNameAndTraits()
    {
        var descriptor = Assert.Single(TestDataExpander.ExpandSingle(new TestDataDescriptor
        {
            BaseId = "Tests.Add",
            TestClass = typeof(Target),
            MethodName = nameof(Target.Add),
            DataSourceName = nameof(GetRows),
            DataSourceType = typeof(VSTestCaseFactoryTests),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)]
        }));

        var testCase = VSTestCaseFactory.Create(descriptor, "tests.dll");
        var traits = testCase.Traits.Select(trait => (trait.Name, trait.Value)).ToArray();

        Assert.Equal(descriptor.Id.Value, testCase.FullyQualifiedName);
        Assert.Equal("adapter row", testCase.DisplayName);
        Assert.Contains(("Category", "Adapter"), traits);
        Assert.Contains(("Tag", "Fast"), traits);
        Assert.Contains(("SkipReason", "Tracked issue"), traits);
    }

    public static IEnumerable<TestDataRow<(int A, int B, int Expected)>> GetRows()
    {
        yield return new TestDataRow<(int A, int B, int Expected)>(
            (1, 2, 3),
            displayName: "adapter row",
            categories: ["Adapter"],
            tags: ["Fast"],
            skipReason: "Tracked issue");
    }

    private sealed class Target
    {
        public void Add(int a, int b, int expected)
        {
        }
    }
}
