
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;

public class EventPersisterTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private sealed record TestEvent(string Name = "test") : IEventPayload;

    private sealed record TestRejectionEvent(string Reason = "rejected") : IRejectionEvent;

    private static CommandEnvelope CreateTestCommand(
        string? correlationId = null,
        string? causationId = null,
        string userId = "user-1") => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? "corr-001",
        CausationId: causationId,
        UserId: userId,
        Extensions: null);

    private static (EventPersister Persister, IActorStateManager StateManager) CreatePersister() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<EventPersister> logger = Substitute.For<ILogger<EventPersister>>();
        return (new EventPersister(stateManager, logger, new NoOpEventPayloadProtectionService()), stateManager);
    }

    private static void ConfigureNoMetadata(IActorStateManager stateManager) => stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

    private static void ConfigureExistingMetadata(IActorStateManager stateManager, long currentSequence) {
        var metadata = new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
    }

    // === 6.1: New aggregate -- first event gets sequence 1, metadata created with CurrentSequence=1 ===

    [Fact]
    public async Task PersistEventsAsync_NewAggregate_FirstEventGetsSequence1() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:1",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PersistEventsAsync_NewAggregate_MetadataCreatedWithSequence1() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.Received(1).SetStateAsync(
            TestIdentity.MetadataKey,
            Arg.Is<AggregateMetadata>(m => m.CurrentSequence == 1),
            Arg.Any<CancellationToken>());
    }

    // === 6.2: Existing aggregate with CurrentSequence=5 -- next event gets sequence 6 ===

    [Fact]
    public async Task PersistEventsAsync_ExistingAggregate_NextEventGetsCorrectSequence() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureExistingMetadata(stateManager, 5);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:6",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 6),
            Arg.Any<CancellationToken>());
    }

    // === 6.3: Multiple events from single command -- sequences are gapless ===

    [Fact]
    public async Task PersistEventsAsync_MultipleEvents_GaplessSequences() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureExistingMetadata(stateManager, 5);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[]
        {
            new TestEvent("first"),
            new TestEvent("second"),
            new TestEvent("third"),
        });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- sequences 6, 7, 8
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:6",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 6),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:7",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 7),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:8",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 8),
            Arg.Any<CancellationToken>());
    }

    // === 6.4: All 11 metadata fields populated correctly (SEC-1) ===

    [Fact]
    public async Task PersistEventsAsync_Populates11MetadataFields() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand(correlationId: "corr-abc", causationId: "cause-xyz", userId: "alice");
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        EventPersistResult result = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v2");

        // Assert -- verify all 15 metadata fields (FR11 + SerializationFormat)
        EventEnvelope envelope = result.PersistedEnvelopes.ShouldHaveSingleItem();
        envelope.MessageId.ShouldNotBeNullOrWhiteSpace();       // 1. MessageId (ULID)
        envelope.AggregateId.ShouldBe("agg-001");               // 2. AggregateId
        envelope.AggregateType.ShouldBe("test-domain");        // 3. AggregateType (domain)
        envelope.TenantId.ShouldBe("test-tenant");               // 4. TenantId
        envelope.Domain.ShouldBe("test-domain");                 // 5. Domain
        envelope.SequenceNumber.ShouldBe(1);                     // 6. SequenceNumber
        envelope.GlobalPosition.ShouldBe(0);                     // 7. GlobalPosition (v1 hardcoded)
        envelope.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue); // 8. Timestamp
        envelope.CorrelationId.ShouldBe("corr-abc");             // 9. CorrelationId
        envelope.CausationId.ShouldBe("cause-xyz");              // 10. CausationId
        envelope.UserId.ShouldBe("alice");                       // 11. UserId
        envelope.DomainServiceVersion.ShouldBe("v2");            // 12. DomainServiceVersion
        envelope.EventTypeName.ShouldContain("TestEvent");       // 13. EventTypeName
        envelope.MetadataVersion.ShouldBe(1);                    // 14. MetadataVersion
        envelope.SerializationFormat.ShouldBe("json");           // 15. SerializationFormat
        envelope.Payload.Length.ShouldBeGreaterThan(0);          // Payload populated
    }

    [Fact]
    public async Task PersistEventsAsync_NullCausationId_UseCorrelationIdAsFallback() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand(correlationId: "corr-fallback", causationId: null);
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.Received(1).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<EventEnvelope>(e => e.CausationId == "corr-fallback"),
            Arg.Any<CancellationToken>());
    }

    // === 6.5: Event payload serialized to JSON bytes ===

    [Fact]
    public async Task PersistEventsAsync_PayloadSerializedToJsonBytes() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent("hello") });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.Received(1).SetStateAsync(
            Arg.Any<string>(),
            Arg.Is<EventEnvelope>(e => e.Payload.Length > 0),
            Arg.Any<CancellationToken>());
    }

    // === 6.6: Event keys follow write-once pattern ===

    [Fact]
    public async Task PersistEventsAsync_EventKeysFollowPattern() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureExistingMetadata(stateManager, 3);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent(), new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- keys match {tenant}:{domain}:{aggId}:events:{seq}
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:4",
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:5",
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    // === 6.7: AggregateMetadata updated with new CurrentSequence and LastModified ===

    [Fact]
    public async Task PersistEventsAsync_MetadataUpdatedWithCorrectSequence() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureExistingMetadata(stateManager, 5);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent(), new TestEvent(), new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- 5 + 3 = 8
        await stateManager.Received(1).SetStateAsync(
            TestIdentity.MetadataKey,
            Arg.Is<AggregateMetadata>(m => m.CurrentSequence == 8 && m.LastModified > DateTimeOffset.MinValue),
            Arg.Any<CancellationToken>());
    }

    // === 6.8: No-op result -- no events persisted ===

    [Fact]
    public async Task PersistEventsAsync_NoOpResult_NoEventsPersisted() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.NoOp();

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- no SetStateAsync calls at all
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().SetStateAsync(
            Arg.Any<string>(),
            Arg.Any<AggregateMetadata>(),
            Arg.Any<CancellationToken>());
    }

    // === 6.9: SaveStateAsync NOT called by EventPersister ===

    [Fact]
    public async Task PersistEventsAsync_DoesNotCallSaveStateAsync() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert
        await stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    // === 6.10: EventPersister never calls RemoveStateAsync (immutability, FR9) ===

    [Fact]
    public async Task PersistEventsAsync_NeverCallsRemoveStateAsync() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureExistingMetadata(stateManager, 3);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Success(new IEventPayload[] { new TestEvent(), new TestEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- immutability: never remove event keys
        await stateManager.DidNotReceive().RemoveStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // === 6.11: Rejection events persisted same as regular events (D3) ===

    [Fact]
    public async Task PersistEventsAsync_RejectionEvents_PersistedLikeRegularEvents() {
        // Arrange
        (EventPersister persister, IActorStateManager stateManager) = CreatePersister();
        ConfigureNoMetadata(stateManager);
        CommandEnvelope command = CreateTestCommand();
        var domainResult = DomainResult.Rejection(new IRejectionEvent[] { new TestRejectionEvent() });

        // Act
        _ = await persister.PersistEventsAsync(TestIdentity, command, domainResult, "v1");

        // Assert -- rejection events also get sequence numbers and are persisted
        await stateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:events:1",
            Arg.Is<EventEnvelope>(e => e.SequenceNumber == 1 && e.EventTypeName.Contains("TestRejectionEvent")),
            Arg.Any<CancellationToken>());
        await stateManager.Received(1).SetStateAsync(
            TestIdentity.MetadataKey,
            Arg.Is<AggregateMetadata>(m => m.CurrentSequence == 1),
            Arg.Any<CancellationToken>());
    }

    // === Guard clause tests ===

    [Fact]
    public async Task PersistEventsAsync_NullIdentity_ThrowsArgumentNullException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            persister.PersistEventsAsync(null!, CreateTestCommand(), DomainResult.NoOp(), "v1"));
    }

    [Fact]
    public async Task PersistEventsAsync_NullCommand_ThrowsArgumentNullException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            persister.PersistEventsAsync(TestIdentity, null!, DomainResult.NoOp(), "v1"));
    }

    [Fact]
    public async Task PersistEventsAsync_NullDomainResult_ThrowsArgumentNullException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            persister.PersistEventsAsync(TestIdentity, CreateTestCommand(), null!, "v1"));
    }

    [Fact]
    public async Task PersistEventsAsync_NullVersion_ThrowsArgumentNullException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentNullException>(() =>
            persister.PersistEventsAsync(TestIdentity, CreateTestCommand(), DomainResult.NoOp(), null!));
    }

    // === M3: Empty/whitespace domainServiceVersion validation ===

    [Fact]
    public async Task PersistEventsAsync_EmptyVersion_ThrowsArgumentException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            persister.PersistEventsAsync(TestIdentity, CreateTestCommand(), DomainResult.NoOp(), ""));
    }

    [Fact]
    public async Task PersistEventsAsync_WhitespaceVersion_ThrowsArgumentException() {
        (EventPersister persister, _) = CreatePersister();
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            persister.PersistEventsAsync(TestIdentity, CreateTestCommand(), DomainResult.NoOp(), "  "));
    }
}
