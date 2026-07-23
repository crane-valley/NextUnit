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
}
