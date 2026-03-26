using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprPubSubOverviewTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        List<DaprComponentDetail> components =
        [
            new DaprComponentDetail("pubsub", "pubsub.redis", DaprComponentCategory.PubSub, "v1", HealthStatus.Healthy, DateTimeOffset.UtcNow, []),
        ];
        List<DaprSubscriptionInfo> subscriptions =
        [
            new DaprSubscriptionInfo("pubsub", "*.*.events", "/events/handle", "DECLARATIVE", null),
        ];

        var overview = new DaprPubSubOverview(components, subscriptions, true);

        overview.PubSubComponents.Count.ShouldBe(1);
        overview.Subscriptions.Count.ShouldBe(1);
        overview.IsRemoteMetadataAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullPubSubComponents_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprPubSubOverview(null!, [], false));
    }

    [Fact]
    public void Constructor_WithNullSubscriptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprPubSubOverview([], null!, false));
    }

    [Fact]
    public void Constructor_WithEmptyCollections_CreatesInstance()
    {
        var overview = new DaprPubSubOverview([], [], false);

        overview.PubSubComponents.ShouldBeEmpty();
        overview.Subscriptions.ShouldBeEmpty();
        overview.IsRemoteMetadataAvailable.ShouldBeFalse();
    }
}
