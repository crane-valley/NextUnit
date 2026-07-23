using Microsoft.Testing.Platform.Extensions.Messages;
using NextUnit.Internal;

namespace NextUnit.Platform;

internal static class TestNodeFactory
{
    public static TestNode Create(
        TestCaseDescriptor descriptor,
        IEnumerable<IProperty>? executionProperties = null)
    {
        var properties = executionProperties?.ToList() ?? new List<IProperty>();

        foreach (var category in descriptor.Categories)
        {
            properties.Add(new TestMetadataProperty("Category", category));
        }

        foreach (var tag in descriptor.Tags)
        {
            properties.Add(new TestMetadataProperty("Tag", tag));
        }

        if (descriptor.SkipReason is not null)
        {
            properties.Add(new TestMetadataProperty("SkipReason", descriptor.SkipReason));
        }

        return new TestNode
        {
            Uid = new TestNodeUid(descriptor.Id.Value),
            DisplayName = descriptor.DisplayName,
            Properties = new PropertyBag(properties)
        };
    }
}
