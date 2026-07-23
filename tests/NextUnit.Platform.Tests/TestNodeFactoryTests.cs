using Microsoft.Testing.Platform.Extensions.Messages;
using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

public sealed class TestNodeFactoryTests
{
    [Test]
    public void Create_PreservesIdentityAndFilterMetadata()
    {
        var descriptor = new TestCaseDescriptor
        {
            Id = new TestCaseId("Tests.Add:Rows[0]"),
            DisplayName = "row display name",
            Categories = ["Row", "Smoke"],
            Tags = ["Fast"],
            IsSkipped = true,
            SkipReason = "Tracked issue"
        };

        var node = TestNodeFactory.Create(descriptor);
        var metadata = node.Properties
            .OfType<TestMetadataProperty>()
            .Select(property => (property.Key, property.Value))
            .ToArray();

        Assert.Equal(descriptor.Id.Value, node.Uid.Value);
        Assert.Equal(descriptor.DisplayName, node.DisplayName);
        Assert.Contains(("Category", "Row"), metadata);
        Assert.Contains(("Category", "Smoke"), metadata);
        Assert.Contains(("Tag", "Fast"), metadata);
        Assert.Contains(("SkipReason", "Tracked issue"), metadata);
    }
}
