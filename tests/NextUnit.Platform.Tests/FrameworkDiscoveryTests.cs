using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace NextUnit.Platform.Tests;

/// <summary>
/// Pins the property that makes a discovery-only request report anything at all.
/// </summary>
/// <remarks>
/// Microsoft.Testing.Platform counts a node as discovered only when it carries
/// <see cref="DiscoveredTestNodeStateProperty"/>. Publishing a node without it reports an empty
/// assembly and exits with code 8, which is what <c>--list-tests</c> and every platform-based IDE
/// discovery saw before this was fixed.
/// </remarks>
public sealed class FrameworkDiscoveryTests
{
    // Constructing the framework reads filter environment variables; see FilterEnvironmentConstraint.
    [Test]
    [NotInParallel(FilterEnvironmentConstraint.Key)]
    public async Task Discovery_MarksEveryPublishedNodeAsDiscoveredAsync()
    {
        // The registry belongs to this test assembly, so the nodes under assertion are the ones a
        // real --list-tests run over this project would produce.
        using var framework = new NextUnitFramework(null!, new NullServiceProvider());
        var messageBus = new RecordingMessageBus();

        await framework.DiscoverAsync(new SessionUid("discovery-test"), messageBus, CancellationToken.None);

        Assert.True(messageBus.TestNodeUpdates.Count > 0);

        foreach (var update in messageBus.TestNodeUpdates)
        {
            var discovered = update.TestNode.Properties.OfType<DiscoveredTestNodeStateProperty>();

            Assert.Equal(1, discovered.Length);
        }
    }

    [Test]
    [NotInParallel(FilterEnvironmentConstraint.Key)]
    public async Task Discovery_PublishesEveryNodeUnderTheGivenSessionAsync()
    {
        using var framework = new NextUnitFramework(null!, new NullServiceProvider());
        var messageBus = new RecordingMessageBus();
        var sessionUid = new SessionUid("session-under-test");

        await framework.DiscoverAsync(sessionUid, messageBus, CancellationToken.None);

        Assert.True(messageBus.TestNodeUpdates.Count > 0);

        foreach (var update in messageBus.TestNodeUpdates)
        {
            Assert.Equal(sessionUid.Value, update.SessionUid.Value);
            Assert.False(string.IsNullOrEmpty(update.TestNode.Uid.Value));
        }
    }
}
