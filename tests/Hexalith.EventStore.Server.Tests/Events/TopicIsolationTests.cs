
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.2 Task 3: Comprehensive multi-tenant topic isolation tests.
/// Verifies that events for different tenants/domains are published to structurally distinct topics
/// with no overlap (AC: #1, #2, #3, #4, #6, #9).
/// </summary>
public class TopicIsolationTests {
    private static EventEnvelope CreateEnvelope(string tenantId, string domain, string aggregateId, long seq = 1) =>
        new(
            AggregateId: aggregateId,
            TenantId: tenantId,
            Domain: domain,
            SequenceNumber: seq,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: $"corr-{tenantId}-{domain}-{seq}",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "TestEvent",
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    // --- Task 3.2: AC #1, #2 ---

    [Fact]
    public async Task PublishEvents_DifferentTenants_SameDomain_UseDifferentTopics() {
        // Arrange
        var identityA = new AggregateIdentity("acme", "orders", "order-1");
        var identityB = new AggregateIdentity("globex", "orders", "order-1");
        var fakePublisher = new FakeEventPublisher();
        IReadOnlyList<EventEnvelope> eventsA = [CreateEnvelope("acme", "orders", "order-1")];
        IReadOnlyList<EventEnvelope> eventsB = [CreateEnvelope("globex", "orders", "order-1")];

        // Act
        _ = await fakePublisher.PublishEventsAsync(identityA, eventsA, "corr-a");
        _ = await fakePublisher.PublishEventsAsync(identityB, eventsB, "corr-b");

        // Assert
        fakePublisher.GetPublishedTopics().ShouldBe(["acme.orders.events", "globex.orders.events"]);
        fakePublisher.GetEventsForTopic("acme.orders.events").ShouldNotBeEmpty();
        fakePublisher.GetEventsForTopic("globex.orders.events").ShouldNotBeEmpty();
    }

    // --- Task 3.3: AC #3 ---

    [Fact]
    public async Task PublishEvents_SameTenant_DifferentDomains_UseDifferentTopics() {
        // Arrange
        var identityOrders = new AggregateIdentity("acme", "orders", "order-1");
        var identityInventory = new AggregateIdentity("acme", "inventory", "inv-1");
        var fakePublisher = new FakeEventPublisher();
        IReadOnlyList<EventEnvelope> eventsOrders = [CreateEnvelope("acme", "orders", "order-1")];
        IReadOnlyList<EventEnvelope> eventsInventory = [CreateEnvelope("acme", "inventory", "inv-1")];

        // Act
        _ = await fakePublisher.PublishEventsAsync(identityOrders, eventsOrders, "corr-orders");
        _ = await fakePublisher.PublishEventsAsync(identityInventory, eventsInventory, "corr-inv");

        // Assert
        fakePublisher.GetPublishedTopics().ShouldBe(["acme.inventory.events", "acme.orders.events"]);
        fakePublisher.GetEventsForTopic("acme.orders.events").ShouldNotBeEmpty();
        fakePublisher.GetEventsForTopic("acme.inventory.events").ShouldNotBeEmpty();
    }

    // --- Task 3.4: AC #2, #3, #4 ---

    [Fact]
    public async Task PublishEvents_DifferentTenants_DifferentDomains_AllDistinctTopics() {
        // Arrange
        var fakePublisher = new FakeEventPublisher();
        (string Tenant, string Domain)[] matrix = new[]
        {
            (Tenant: "acme", Domain: "orders"),
            (Tenant: "acme", Domain: "inventory"),
            (Tenant: "globex", Domain: "orders"),
            (Tenant: "globex", Domain: "inventory"),
        };

        // Act
        foreach ((string tenant, string domain) in matrix) {
            var identity = new AggregateIdentity(tenant, domain, "agg-1");
            IReadOnlyList<EventEnvelope> events = [CreateEnvelope(tenant, domain, "agg-1")];
            _ = await fakePublisher.PublishEventsAsync(identity, events, $"corr-{tenant}-{domain}");
        }

        // Assert
        IReadOnlyList<string> topics = fakePublisher.GetPublishedTopics();
        topics.Count.ShouldBe(4);
        topics.ShouldContain("acme.orders.events");
        topics.ShouldContain("acme.inventory.events");
        topics.ShouldContain("globex.orders.events");
        topics.ShouldContain("globex.inventory.events");

        // Cross-tenant isolation: each topic has exactly 1 event
        foreach (string topic in topics) {
            fakePublisher.GetEventsForTopic(topic).Count.ShouldBe(1);
        }
    }

    // --- Task 3.5: AC #6 ---

    [Fact]
    public async Task PublishEvents_SameTenantDomain_MultipleCalls_SameTopic() {
        // Arrange
        var identity = new AggregateIdentity("acme", "orders", "order-1");
        var fakePublisher = new FakeEventPublisher();

        // Act
        for (int i = 1; i <= 5; i++) {
            IReadOnlyList<EventEnvelope> events = [CreateEnvelope("acme", "orders", "order-1", i)];
            _ = await fakePublisher.PublishEventsAsync(identity, events, $"corr-{i}");
        }

        // Assert - all 5 calls go to the same single topic
        fakePublisher.GetPublishedTopics().ShouldBe(["acme.orders.events"]);
        fakePublisher.GetEventsForTopic("acme.orders.events").Count.ShouldBe(5);
    }

    // --- Task 3.6: AC #6 ---

    [Fact]
    public async Task PublishEvents_CaseNormalization_ProducesSameTopic() {
        // Arrange - AggregateIdentity forces lowercase
        var identityLower = new AggregateIdentity("acme", "orders", "order-1");
        var identityMixed = new AggregateIdentity("Acme", "Orders", "order-1");
        var identityUpper = new AggregateIdentity("ACME", "ORDERS", "order-1");
        var fakePublisher = new FakeEventPublisher();

        // Act
        IReadOnlyList<EventEnvelope> events = [CreateEnvelope("acme", "orders", "order-1")];
        _ = await fakePublisher.PublishEventsAsync(identityLower, events, "corr-lower");
        _ = await fakePublisher.PublishEventsAsync(identityMixed, events, "corr-mixed");
        _ = await fakePublisher.PublishEventsAsync(identityUpper, events, "corr-upper");

        // Assert - all produce the same topic due to lowercase enforcement
        fakePublisher.GetPublishedTopics().ShouldBe(["acme.orders.events"]);
        fakePublisher.GetEventsForTopic("acme.orders.events").Count.ShouldBe(3);
    }

    // --- Task 3.7: AC #9 ---

    [Fact]
    public async Task PublishEvents_ConcurrentTenants_NoTopicCrossContamination() {
        // Arrange
        var fakePublisher = new FakeEventPublisher();
        string[] tenants = new[] { "tenant-a", "tenant-b", "tenant-c", "tenant-d", "tenant-e" };

        // Act - publish concurrently
        Task[] tasks = tenants.Select(tenant => {
            var identity = new AggregateIdentity(tenant, "orders", "order-1");
            IReadOnlyList<EventEnvelope> events = [
                CreateEnvelope(tenant, "orders", "order-1", 1),
                CreateEnvelope(tenant, "orders", "order-1", 2),
            ];
            return fakePublisher.PublishEventsAsync(identity, events, $"corr-{tenant}");
        }).ToArray();
        await Task.WhenAll(tasks);

        // Assert
        IReadOnlyList<string> topics = fakePublisher.GetPublishedTopics();
        topics.Count.ShouldBe(5);

        foreach (string tenant in tenants) {
            string expectedTopic = $"{tenant}.orders.events";
            topics.ShouldContain(expectedTopic);

            IReadOnlyList<EventEnvelope> topicEvents = fakePublisher.GetEventsForTopic(expectedTopic);
            topicEvents.Count.ShouldBe(2);

            // Verify all events on this topic belong to the correct tenant
            foreach (EventEnvelope envelope in topicEvents) {
                envelope.TenantId.ShouldBe(tenant);
            }
        }
    }

    // --- Task 3.8 ---

    [Fact]
    public async Task PublishEvents_TenantWithHyphens_ValidTopic() {
        // Arrange
        var identity = new AggregateIdentity("acme-corp", "order-service", "order-1");
        var fakePublisher = new FakeEventPublisher();
        IReadOnlyList<EventEnvelope> events = [CreateEnvelope("acme-corp", "order-service", "order-1")];

        // Act
        _ = await fakePublisher.PublishEventsAsync(identity, events, "corr-1");

        // Assert
        fakePublisher.GetPublishedTopics().ShouldBe(["acme-corp.order-service.events"]);
    }

    // --- Task 3.9 ---

    [Fact]
    public async Task PublishEvents_SingleCharTenantDomain_ValidTopic() {
        // Arrange
        var identity = new AggregateIdentity("a", "b", "order-1");
        var fakePublisher = new FakeEventPublisher();
        IReadOnlyList<EventEnvelope> events = [CreateEnvelope("a", "b", "order-1")];

        // Act
        _ = await fakePublisher.PublishEventsAsync(identity, events, "corr-1");

        // Assert
        fakePublisher.GetPublishedTopics().ShouldBe(["a.b.events"]);
    }

    // --- Task 3.10 ---

    [Fact]
    public async Task PublishEvents_MaxLengthTenantDomain_ValidTopic() {
        // Arrange - 64-char tenant + 64-char domain
        string longTenant = new('a', 64);
        string longDomain = new('b', 64);
        var identity = new AggregateIdentity(longTenant, longDomain, "order-1");
        var fakePublisher = new FakeEventPublisher();
        IReadOnlyList<EventEnvelope> events = [CreateEnvelope(longTenant, longDomain, "order-1")];

        // Act
        _ = await fakePublisher.PublishEventsAsync(identity, events, "corr-1");

        // Assert - topic is {64}.{64}.events = 136 chars (64+1+64+1+6), well within all backend limits
        string expectedTopic = $"{longTenant}.{longDomain}.events";
        expectedTopic.Length.ShouldBe(136);
        fakePublisher.GetPublishedTopics().ShouldBe([expectedTopic]);
    }
}
