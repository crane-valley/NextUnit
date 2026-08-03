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
}
