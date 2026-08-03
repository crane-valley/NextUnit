using NextUnit.Internal;
using Xunit;

namespace NextUnit.TestAdapter.Tests;

public sealed class NextUnitTestExecutorTests
{
    [Fact]
    public void BuildSelectedDescriptorIds_IndexesDynamicBaseIds()
    {
        var selectedIds = new[]
        {
            "Tests.Static",
            "Tests.MemberData:Rows[42]",
            "Tests.ClassData:ClassData:Source[7]",
            "Tests.Combined:Combined[3]"
        };

        var descriptorIds = NextUnitTestExecutor.BuildSelectedDescriptorIds(selectedIds);

        Assert.Contains("Tests.Static", descriptorIds);
        Assert.Contains("Tests.MemberData", descriptorIds);
        Assert.Contains("Tests.ClassData", descriptorIds);
        Assert.Contains("Tests.Combined", descriptorIds);
        Xunit.Assert.DoesNotContain("Tests.Member", descriptorIds);
        Assert.Equal(7, descriptorIds.Count);
    }

    /// <summary>
    /// A deferred data source is discovered as one placeholder whose id is the row prefix without an
    /// index, so selecting it in an IDE has to map back to the descriptor exactly as a row id does.
    /// Without that, selecting the placeholder would expand no descriptor and run nothing.
    /// </summary>
    [Fact]
    public void BuildSelectedDescriptorIds_MapsDeferredPlaceholderToItsDescriptor()
    {
        var descriptorIds = NextUnitTestExecutor.BuildSelectedDescriptorIds(
            ["Tests.Deferred:TestProject.Tests.Rows"]);

        Assert.Contains("Tests.Deferred", descriptorIds);
        Assert.Contains("Tests.Deferred:TestProject.Tests.Rows", descriptorIds);
        Assert.Equal(2, descriptorIds.Count);
    }

    /// <summary>
    /// A deferred row's id exists only after a run has produced it, but VSTest keeps the test cases
    /// it saw in results, so the user can select one and run it again. Discovery still offers only
    /// the placeholder, so an exact-id filter would drop it and the rerun would silently do nothing.
    /// </summary>
    [Fact]
    public void StandsForSelectedRow_PlaceholderOfASelectedRow_IsRetained()
    {
        var placeholder = CreateDeferredPlaceholder();
        var selected = new HashSet<string>(StringComparer.Ordinal)
        {
            $"{placeholder.Id.Value}[1]"
        };

        Assert.True(NextUnitTestExecutor.StandsForSelectedRow(placeholder, selected));
    }

    [Fact]
    public void StandsForSelectedRow_PlaceholderOfAnUnrelatedSelection_IsNotRetained()
    {
        var placeholder = CreateDeferredPlaceholder();
        var selected = new HashSet<string>(StringComparer.Ordinal)
        {
            "Tests.Other:TestProject.Tests.OtherRows[0]",

            // Shares the placeholder id as a string prefix but is a different data source, so the
            // "[" in the row prefix is what keeps it out.
            $"{placeholder.Id.Value}Extra[0]"
        };

        Assert.False(NextUnitTestExecutor.StandsForSelectedRow(placeholder, selected));
    }

    /// <summary>
    /// The prefix rule applies only to placeholders. An ordinary test case is selected by its exact
    /// id and must never be pulled into a run by a row id that happens to extend it.
    /// </summary>
    [Fact]
    public void StandsForSelectedRow_OrdinaryTestCase_IsNotRetained()
    {
        var testCase = new TestCaseDescriptor
        {
            Id = new TestCaseId("Tests.Ordinary"),
            DisplayName = "Ordinary"
        };
        var selected = new HashSet<string>(StringComparer.Ordinal) { "Tests.Ordinary[0]" };

        Assert.False(NextUnitTestExecutor.StandsForSelectedRow(testCase, selected));
    }

    private static TestCaseDescriptor CreateDeferredPlaceholder() =>
        TestDataExpander.ExpandSingle(
            new TestDataDescriptor
            {
                BaseId = "Tests.Deferred",
                DisplayName = "Deferred",
                TestClass = typeof(DeferredTarget),
                MethodName = nameof(DeferredTarget.Run),
                DataSourceName = "Rows",
                DataSourceType = typeof(NextUnitTestExecutorTests),
                DeferredEnumeration = true,
                DataSourceProvider = static () => Array.Empty<object[]>()
            },
            CancellationToken.None).Single();

    private sealed class DeferredTarget
    {
        public void Run(int value)
        {
        }
    }
}
