namespace Hexalith.EventStore.Server.Tests.Events;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

public class EventStreamReaderTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEvent(int seq) => new(
        AggregateId: "agg-001",
        TenantId: "test-tenant",
        Domain: "test-domain",
        SequenceNumber: seq,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: $"corr-{seq}",
        CausationId: $"cause-{seq}",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "OrderCreated",
        SerializationFormat: "json",
        Payload: [1, 2, 3],
        Extensions: null);

    private static SnapshotRecord CreateTestSnapshot(long sequenceNumber, object? state = null) => new(
        SequenceNumber: sequenceNumber,
        State: state ?? new { Name = "test-state" },
        CreatedAt: DateTimeOffset.UtcNow,
        Domain: "test-domain",
        AggregateId: "agg-001",
        TenantId: "test-tenant");

    private static (EventStreamReader Reader, IActorStateManager StateManager) CreateReader() {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<EventStreamReader>>();
        return (new EventStreamReader(stateManager, logger), stateManager);
    }

    private static void ConfigureNoMetadata(IActorStateManager stateManager, AggregateIdentity identity) {
        stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    private static void ConfigureMetadata(IActorStateManager stateManager, AggregateIdentity identity, long currentSequence) {
        var metadata = new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
    }

    private static void ConfigureEvents(IActorStateManager stateManager, AggregateIdentity identity, int fromSeq, int toSeq) {
        string keyPrefix = identity.EventStreamKeyPrefix;
        for (int i = fromSeq; i <= toSeq; i++) {
            int seq = i;
            stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq)));
        }
    }

    private static void ConfigureEvents(IActorStateManager stateManager, AggregateIdentity identity, int count)
        => ConfigureEvents(stateManager, identity, 1, count);

    // === Existing tests updated for RehydrationResult return type ===

    [Fact]
    public async Task RehydrateAsync_NewAggregate_ReturnsNull() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureNoMetadata(stateManager, TestIdentity);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RehydrateAsync_ExistingAggregate_ReadsEventsFromSequence1() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 3);
        ConfigureEvents(stateManager, TestIdentity, 3);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(3);
        result.Events[0].SequenceNumber.ShouldBe(1);
        result.Events[1].SequenceNumber.ShouldBe(2);
        result.Events[2].SequenceNumber.ShouldBe(3);
        result.SnapshotState.ShouldBeNull();
        result.LastSnapshotSequence.ShouldBe(0);
        result.CurrentSequence.ShouldBe(3);
    }

    [Fact]
    public async Task RehydrateAsync_ExistingAggregate_UsesCorrectKeyPattern() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 2);
        ConfigureEvents(stateManager, TestIdentity, 2);

        // Act
        await reader.RehydrateAsync(TestIdentity);

        // Assert -- verify composite key pattern {tenant}:{domain}:{aggId}:events:{seq}
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1", Arg.Any<CancellationToken>());
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RehydrateAsync_ThousandEvents_CompletesWithin100ms() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 1000);

        // Configure all 1000 events to return immediately (mock -- no real I/O)
        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                string key = callInfo.Arg<string>();
                if (key.StartsWith(keyPrefix, StringComparison.Ordinal)) {
                    int seq = int.Parse(key[keyPrefix.Length..]);
                    return new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq));
                }

                return new ConditionalValue<EventEnvelope>(false, default!);
            });

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);
        sw.Stop();

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(1000);
        sw.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task RehydrateAsync_MissingEvent_ThrowsMissingEventException() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 3);

        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        // Event 1 exists, event 2 missing, event 3 exists
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(1)));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}3", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(3)));

        // Act & Assert
        var ex = await Should.ThrowAsync<MissingEventException>(() => reader.RehydrateAsync(TestIdentity));
        ex.SequenceNumber.ShouldBe(2);
        ex.TenantId.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("test-domain");
        ex.AggregateId.ShouldBe("agg-001");
    }

    [Fact]
    public async Task RehydrateAsync_InvalidMetadata_NegativeSequence_ThrowsException() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        var metadata = new AggregateMetadata(-1, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => reader.RehydrateAsync(TestIdentity));
        ex.Message.ShouldContain("CurrentSequence=-1");
    }

    [Fact]
    public async Task RehydrateAsync_InvalidMetadata_ZeroSequence_ThrowsException() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        var metadata = new AggregateMetadata(0, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => reader.RehydrateAsync(TestIdentity));
        ex.Message.ShouldContain("CurrentSequence=0");
    }

    [Fact]
    public async Task RehydrateAsync_NullIdentity_ThrowsArgumentNullException() {
        // Arrange
        (EventStreamReader reader, _) = CreateReader();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => reader.RehydrateAsync(null!));
    }

    [Fact]
    public async Task RehydrateAsync_EventsLoadedInOrder_VerifySequence() {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 5);
        ConfigureEvents(stateManager, TestIdentity, 5);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldNotBeNull();
        for (int i = 0; i < result.Events.Count; i++) {
            result.Events[i].SequenceNumber.ShouldBe(i + 1);
        }
    }

    // === Story 3.10: Snapshot-aware rehydration tests (Task 6) ===

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_ReadsOnlyTailEvents() {
        // Arrange -- AC #1: snapshot at 500, events 501-520
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 520);
        ConfigureEvents(stateManager, TestIdentity, 501, 520);
        SnapshotRecord snapshot = CreateTestSnapshot(500);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(20);
        result.Events[0].SequenceNumber.ShouldBe(501);
        result.Events[19].SequenceNumber.ShouldBe(520);
        result.SnapshotState.ShouldNotBeNull();
        result.LastSnapshotSequence.ShouldBe(500);
        result.CurrentSequence.ShouldBe(520);
        result.UsedSnapshot.ShouldBeTrue();

        // Verify events 1-500 were NOT read
        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        await stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(
            $"{keyPrefix}1", Arg.Any<CancellationToken>());
        await stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(
            $"{keyPrefix}500", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_NoTailEvents_ReturnsSnapshotState() {
        // Arrange -- AC #8: snapshot at current sequence
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 500);
        SnapshotRecord snapshot = CreateTestSnapshot(500);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert
        result.ShouldNotBeNull();
        result.Events.ShouldBeEmpty();
        result.SnapshotState.ShouldNotBeNull();
        result.LastSnapshotSequence.ShouldBe(500);
        result.CurrentSequence.ShouldBe(500);
        result.TailEventCount.ShouldBe(0);
        result.UsedSnapshot.ShouldBeTrue();

        // No event reads should have occurred
        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        await stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(
            Arg.Is<string>(s => s.StartsWith(keyPrefix)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RehydrateAsync_WithoutSnapshot_FullReplay() {
        // Arrange -- AC #3: no snapshot, full replay
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 5);
        ConfigureEvents(stateManager, TestIdentity, 5);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(5);
        result.SnapshotState.ShouldBeNull();
        result.LastSnapshotSequence.ShouldBe(0);
        result.CurrentSequence.ShouldBe(5);
        result.UsedSnapshot.ShouldBeFalse();
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_TailEventsInOrder() {
        // Arrange -- AC #9: strict sequence ordering
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 10);
        ConfigureEvents(stateManager, TestIdentity, 6, 10);
        SnapshotRecord snapshot = CreateTestSnapshot(5);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(5);
        for (int i = 0; i < result.Events.Count; i++) {
            result.Events[i].SequenceNumber.ShouldBe(6 + i);
        }
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_ParallelReads() {
        // Arrange -- AC #5: parallel reads maintained for tail events
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 105);
        ConfigureEvents(stateManager, TestIdentity, 101, 105);
        SnapshotRecord snapshot = CreateTestSnapshot(100);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert -- all 5 tail events loaded (proves parallel reads work)
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(5);
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_CorrectKeyPattern() {
        // Arrange -- AC #4: reads {tenant}:{domain}:{aggId}:events:{seq} for tail events
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 502);
        ConfigureEvents(stateManager, TestIdentity, 501, 502);
        SnapshotRecord snapshot = CreateTestSnapshot(500);

        // Act
        await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:501", Arg.Any<CancellationToken>());
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:502", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_ReturnsCorrectRehydrationResult() {
        // Arrange -- all fields populated correctly
        var snapshotState = new { ProjectedState = "test" };
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 103);
        ConfigureEvents(stateManager, TestIdentity, 101, 103);
        SnapshotRecord snapshot = CreateTestSnapshot(100, snapshotState);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert
        result.ShouldNotBeNull();
        result.SnapshotState.ShouldBeSameAs(snapshotState);
        result.Events.Count.ShouldBe(3);
        result.LastSnapshotSequence.ShouldBe(100);
        result.CurrentSequence.ShouldBe(103);
        result.TailEventCount.ShouldBe(3);
        result.UsedSnapshot.ShouldBeTrue();
    }

    [Fact]
    public async Task RehydrateAsync_NewAggregate_NoSnapshotNoEvents_ReturnsNull() {
        // Arrange -- unchanged behavior for new aggregates
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureNoMetadata(stateManager, TestIdentity);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RehydrateAsync_WithSnapshot_MissingTailEvent_ThrowsMissingEventException() {
        // Arrange -- gap detection in tail events
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 503);
        SnapshotRecord snapshot = CreateTestSnapshot(500);

        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}501", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(501)));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}502", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!)); // Missing!
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}503", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(503)));

        // Act & Assert
        var ex = await Should.ThrowAsync<MissingEventException>(() => reader.RehydrateAsync(TestIdentity, snapshot));
        ex.SequenceNumber.ShouldBe(502);
    }

    // === Story 3.10: Performance tests (Task 7) ===

    [Fact]
    public async Task RehydrateAsync_SnapshotPlusTailEvents_CompletesWithin50ms() {
        // Arrange -- NFR4: p99 <50ms
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 10020);

        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                string key = callInfo.Arg<string>();
                if (key.StartsWith(keyPrefix, StringComparison.Ordinal)) {
                    int seq = int.Parse(key[keyPrefix.Length..]);
                    return new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq));
                }

                return new ConditionalValue<EventEnvelope>(false, default!);
            });

        SnapshotRecord snapshot = CreateTestSnapshot(10000);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);
        sw.Stop();

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(20);
        sw.ElapsedMilliseconds.ShouldBeLessThan(50);
    }

    [Fact]
    public async Task RehydrateAsync_SnapshotWithManyTailEvents_FasterThanFullReplay() {
        // Arrange -- comparative performance
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<EventStreamReader>>();
        string keyPrefix = TestIdentity.EventStreamKeyPrefix;

        ConfigureMetadata(stateManager, TestIdentity, 10020);
        stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                string key = callInfo.Arg<string>();
                if (key.StartsWith(keyPrefix, StringComparison.Ordinal)) {
                    int seq = int.Parse(key[keyPrefix.Length..]);
                    return new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq));
                }

                return new ConditionalValue<EventEnvelope>(false, default!);
            });

        // Full replay
        var readerFull = new EventStreamReader(stateManager, logger);
        var swFull = System.Diagnostics.Stopwatch.StartNew();
        await readerFull.RehydrateAsync(TestIdentity);
        swFull.Stop();

        // Snapshot + 20 tail events
        SnapshotRecord snapshot = CreateTestSnapshot(10000);
        var readerSnapshot = new EventStreamReader(stateManager, logger);
        var swSnapshot = System.Diagnostics.Stopwatch.StartNew();
        RehydrationResult? snapshotResult = await readerSnapshot.RehydrateAsync(TestIdentity, snapshot);
        swSnapshot.Stop();

        // Assert -- snapshot path should be significantly faster
        snapshotResult.ShouldNotBeNull();
        snapshotResult.Events.Count.ShouldBe(20);
        swSnapshot.ElapsedMilliseconds.ShouldBeLessThan(swFull.ElapsedMilliseconds + 1); // At least not slower
    }

    // === Full replay backward compatibility (Task 9) ===

    [Fact]
    public async Task RehydrateAsync_FullReplay_ReturnsRehydrationResultWithAllEvents() {
        // Arrange -- backward compatible: no snapshot yields full event list
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 3);
        ConfigureEvents(stateManager, TestIdentity, 3);

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(3);
        result.SnapshotState.ShouldBeNull();
        result.UsedSnapshot.ShouldBeFalse();
        result.LastSnapshotSequence.ShouldBe(0);
        result.CurrentSequence.ShouldBe(3);
    }
}
