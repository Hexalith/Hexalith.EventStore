
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;
/// <summary>
/// Validates declarative subscription dead-letter configuration for Story 4.3 AC #3.
/// </summary>
public class SubscriptionConfigurationTests {
    private static readonly string LocalSubscriptionPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "subscription-sample-counter.yaml"));

    private static readonly string ProductionSubscriptionPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "subscription-sample-counter.yaml"));

    [Fact]
    public void LocalSubscription_ContainsDeadLetterTopicConfiguration() {
        string content = File.ReadAllText(LocalSubscriptionPath);

        content.ShouldContain("kind: Subscription");
        content.ShouldContain("pubsubname: pubsub");
        content.ShouldContain("topic: sample.counter.events");
        content.ShouldContain("default: /events/idempotency-demo");
        content.ShouldContain("deadLetterTopic: deadletter.sample.counter.events");
    }

    [Fact]
    public void ProductionSubscription_ContainsDeadLetterTopicConfiguration() {
        string content = File.ReadAllText(ProductionSubscriptionPath);

        content.ShouldContain("kind: Subscription");
        content.ShouldContain("pubsubname: pubsub");
        content.ShouldContain("topic: sample.counter.events");
        content.ShouldContain("default: /events/idempotency-demo");
        content.ShouldContain("deadLetterTopic: deadletter.sample.counter.events");
    }
}
