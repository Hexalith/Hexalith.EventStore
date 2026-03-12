
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.3 Task 8: Subscriber idempotency contract tests.
/// Verifies that CloudEvents id = "{correlationId}:{sequenceNumber}" is globally unique
/// and deterministic, enabling subscriber deduplication for at-least-once delivery.
/// </summary>
public class SubscriberIdempotencyTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1, string correlationId = "corr-001") =>
        new(
            AggregateId: "agg-001",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: correlationId,
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static (EventPublisher Publisher, DaprClient DaprClient) CreatePublisher() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService());
        return (publisher, daprClient);
    }

    // --- Task 8.2: CloudEvents id unique per event ---

    [Fact]
    public async Task CloudEventsId_UniquePerEvent_CorrelationIdPlusSequence() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var capturedMetadata = new List<Dictionary<string, string>>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedMetadata.Add(ci.ArgAt<Dictionary<string, string>>(3)));

        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1),
            CreateTestEnvelope(2),
            CreateTestEnvelope(3),
        };

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, events, "corr-001");

        // Assert -- all ids are unique
        var ids = capturedMetadata.Select(m => m["cloudevent.id"]).ToList();
        ids.Count.ShouldBe(3);
        ids.Distinct().Count().ShouldBe(3, "CloudEvents ids must be globally unique per event");

        // Format: {correlationId}:{sequenceNumber}
        ids[0].ShouldBe("corr-001:1");
        ids[1].ShouldBe("corr-001:2");
        ids[2].ShouldBe("corr-001:3");
    }

    // --- Task 8.3: Same event published twice produces same id ---

    [Fact]
    public async Task CloudEventsId_SameEventPublishedTwice_IdenticalId() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var capturedIds = new List<string>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedIds.Add(ci.ArgAt<Dictionary<string, string>>(3)["cloudevent.id"]));

        EventEnvelope envelope = CreateTestEnvelope(sequenceNumber: 5, correlationId: "corr-dedup");

        // Act -- publish the same event twice (simulating at-least-once redelivery)
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-dedup");
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-dedup");

        // Assert -- deterministic: same event = same id
        capturedIds.Count.ShouldBe(2);
        capturedIds[0].ShouldBe(capturedIds[1],
            "Re-publishing the same event must produce the same CloudEvents id for subscriber dedup");
        capturedIds[0].ShouldBe("corr-dedup:5");
    }

    // --- Task 8.4: Different events same correlation = different ids ---

    [Fact]
    public async Task CloudEventsId_DifferentEvents_SameCorrelation_DifferentIds() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var capturedIds = new List<string>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedIds.Add(ci.ArgAt<Dictionary<string, string>>(3)["cloudevent.id"]));

        // Events from the same command (same correlationId) but different sequence numbers
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1, "same-corr"),
            CreateTestEnvelope(2, "same-corr"),
        };

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, events, "same-corr");

        // Assert
        capturedIds[0].ShouldNotBe(capturedIds[1],
            "Events with same correlation but different sequences must have different ids");
        capturedIds[0].ShouldBe("same-corr:1");
        capturedIds[1].ShouldBe("same-corr:2");
    }

    // --- Task 8.5: Different correlations same sequence = different ids ---

    [Fact]
    public async Task CloudEventsId_DifferentCorrelations_SameSequence_DifferentIds() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var capturedIds = new List<string>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedIds.Add(ci.ArgAt<Dictionary<string, string>>(3)["cloudevent.id"]));

        EventEnvelope envelope1 = CreateTestEnvelope(sequenceNumber: 1, correlationId: "corr-A");
        EventEnvelope envelope2 = CreateTestEnvelope(sequenceNumber: 1, correlationId: "corr-B");

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope1], "corr-A");
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope2], "corr-B");

        // Assert
        capturedIds[0].ShouldNotBe(capturedIds[1],
            "Events with different correlations but same sequence must have different ids");
        capturedIds[0].ShouldBe("corr-A:1");
        capturedIds[1].ShouldBe("corr-B:1");
    }
}
