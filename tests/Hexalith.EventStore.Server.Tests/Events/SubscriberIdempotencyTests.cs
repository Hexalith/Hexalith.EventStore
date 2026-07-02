
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.3 Task 8: Subscriber idempotency contract tests.
/// Verifies that CloudEvents id is the persisted event message id, enabling deterministic
/// subscriber deduplication without aggregate-local sequence collisions.
/// </summary>
public class SubscriberIdempotencyTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(
        long sequenceNumber = 1,
        string correlationId = "corr-001",
        string? messageId = null,
        string aggregateId = "agg-001") =>
        new(
            MessageId: messageId ?? $"msg-{sequenceNumber}",
            AggregateId: aggregateId,
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: correlationId,
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static (EventPublisher Publisher, DaprClient DaprClient) CreatePublisher() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator());
        return (publisher, daprClient);
    }

    // --- Task 8.2: CloudEvents id unique per event ---

    [Fact]
    public async Task CloudEventsId_UniquePerEvent_UsesMessageId() {
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

        ids.ShouldBe(["msg-1", "msg-2", "msg-3"]);
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
        capturedIds[0].ShouldBe("msg-5");
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
        capturedIds[0].ShouldBe("msg-1");
        capturedIds[1].ShouldBe("msg-2");
    }

    // --- Task 8.5: Different events same sequence = different ids ---

    [Fact]
    public async Task CloudEventsId_DifferentAggregatesSameCorrelationAndSequence_DifferentIds() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var capturedIds = new List<string>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => capturedIds.Add(ci.ArgAt<Dictionary<string, string>>(3)["cloudevent.id"]));

        EventEnvelope envelope1 = CreateTestEnvelope(sequenceNumber: 1, correlationId: "same-corr", messageId: "msg-A", aggregateId: "agg-A");
        EventEnvelope envelope2 = CreateTestEnvelope(sequenceNumber: 1, correlationId: "same-corr", messageId: "msg-B", aggregateId: "agg-B");

        // Act
        _ = await publisher.PublishEventsAsync(new AggregateIdentity("test-tenant", "test-domain", "agg-A"), [envelope1], "same-corr");
        _ = await publisher.PublishEventsAsync(new AggregateIdentity("test-tenant", "test-domain", "agg-B"), [envelope2], "same-corr");

        // Assert
        capturedIds[0].ShouldNotBe(capturedIds[1],
            "Events with the same correlation and sequence but different persisted message ids must have different CloudEvents ids");
        capturedIds[0].ShouldBe("msg-A");
        capturedIds[1].ShouldBe("msg-B");
    }
}
