
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;
/// <summary>
/// Story 4.3 Task 10: EventPublisherOptions dead-letter topic configuration tests.
/// Verifies dead-letter topic naming convention: {prefix}.{pubSubTopic}
/// </summary>
public class EventPublisherOptionsTests {
    // --- Task 10.2: Default dead-letter prefix ---

    [Fact]
    public void DeadLetterTopicPrefix_Default_IsDeadletter() {
        // Arrange & Act
        var options = new EventPublisherOptions();

        // Assert
        options.DeadLetterTopicPrefix.ShouldBe("deadletter");
    }

    // --- Task 10.3: GetDeadLetterTopic returns correct pattern ---

    [Fact]
    public void GetDeadLetterTopic_ValidIdentity_ReturnsCorrectPattern() {
        // Arrange
        var options = new EventPublisherOptions();
        var identity = new AggregateIdentity("acme", "orders", "order-001");

        // Act
        string deadLetterTopic = options.GetDeadLetterTopic(identity);

        // Assert -- format: deadletter.{tenantId}.{domain}.events for tenant-scoped identities
        deadLetterTopic.ShouldBe("deadletter.acme.orders.events");
    }

    [Fact]
    public void GetDeadLetterTopic_SystemTenant_ReturnsDomainTopic() {
        // Arrange
        var options = new EventPublisherOptions();
        var identity = new AggregateIdentity("system", "tenants", "global-administrators");

        // Act
        string deadLetterTopic = options.GetDeadLetterTopic(identity);

        // Assert
        deadLetterTopic.ShouldBe("deadletter.tenants.events");
    }

    [Fact]
    public void GetPubSubTopic_WithDomainOverride_ReturnsConfiguredTopic() {
        // Arrange
        var options = new EventPublisherOptions {
            TopicOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["global-administrators"] = "tenants.events",
            },
        };
        var identity = new AggregateIdentity("system", "global-administrators", "global-administrators");

        // Act
        string topic = options.GetPubSubTopic(identity);

        // Assert
        topic.ShouldBe("tenants.events");
        identity.PubSubTopic.ShouldBe("global-administrators.events");
    }

    [Fact]
    public void GetDeadLetterTopic_WithDomainOverride_UsesConfiguredTopic() {
        // Arrange
        var options = new EventPublisherOptions {
            TopicOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["global-administrators"] = "tenants.events",
            },
        };
        var identity = new AggregateIdentity("system", "global-administrators", "global-administrators");

        // Act
        string deadLetterTopic = options.GetDeadLetterTopic(identity);

        // Assert
        deadLetterTopic.ShouldBe("deadletter.tenants.events");
    }

    // --- Task 10.4: Different tenants = different dead-letter topics ---

    [Fact]
    public void GetDeadLetterTopic_DifferentTenants_DifferentTopics() {
        // Arrange
        var options = new EventPublisherOptions();
        var identityA = new AggregateIdentity("tenant-a", "orders", "order-001");
        var identityB = new AggregateIdentity("tenant-b", "orders", "order-001");

        // Act
        string topicA = options.GetDeadLetterTopic(identityA);
        string topicB = options.GetDeadLetterTopic(identityB);

        // Assert -- tenant isolation in dead-letter topics
        topicA.ShouldNotBe(topicB);
        topicA.ShouldBe("deadletter.tenant-a.orders.events");
        topicB.ShouldBe("deadletter.tenant-b.orders.events");
    }

    [Fact]
    public void GetDeadLetterTopic_CustomPrefix_UsesConfiguredPrefix() {
        // Arrange
        var options = new EventPublisherOptions { DeadLetterTopicPrefix = "dlq" };
        var identity = new AggregateIdentity("acme", "orders", "order-001");

        // Act
        string topic = options.GetDeadLetterTopic(identity);

        // Assert
        topic.ShouldBe("dlq.acme.orders.events");
    }

    [Fact]
    public void GetDeadLetterTopic_NullIdentity_ThrowsArgumentNullException() {
        // Arrange
        var options = new EventPublisherOptions();

        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => options.GetDeadLetterTopic(null!));
    }

    [Fact]
    public void PubSubName_Default_IsPubsub() {
        // Arrange & Act
        var options = new EventPublisherOptions();

        // Assert -- verify existing default is preserved
        options.PubSubName.ShouldBe("pubsub");
    }
}
