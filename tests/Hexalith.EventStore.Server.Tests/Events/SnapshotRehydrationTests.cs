
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Integration tests for AggregateActor snapshot-based rehydration (AC: #6, #10).
/// Tests the integration between SnapshotManager snapshot loading and EventStreamReader
/// snapshot-aware rehydration through the event persistence pipeline using InMemoryStateManager.
/// </summary>
public class SnapshotRehydrationTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateEvent(int seq) => new(
        "agg-001", "test-tenant", "test-domain", seq, DateTimeOffset.UtcNow,
        $"corr-{seq}", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", "json",
        [1, 2, 3], null);

    private static async Task<InMemoryStateManager> SetupStateWithEvents(int eventCount) {
        var stateManager = new InMemoryStateManager();

        var metadata = new AggregateMetadata(eventCount, DateTimeOffset.UtcNow, null);
        await stateManager.SetStateAsync(TestIdentity.MetadataKey, metadata).ConfigureAwait(false);

        for (int i = 1; i <= eventCount; i++) {
            await stateManager.SetStateAsync(
                $"{TestIdentity.EventStreamKeyPrefix}{i}",
                CreateEvent(i)).ConfigureAwait(false);
        }

        await stateManager.SaveStateAsync().ConfigureAwait(false);
        return stateManager;
    }

    // --- 8.1: Persist events to trigger snapshot, then verify rehydration uses snapshot for tail-only reads ---

    [Fact]
    public async Task RehydrateWithSnapshot_ReadsOnlyTailEvents_NotAllEvents() {
        // Arrange - state has 10 events, snapshot at seq 7
        InMemoryStateManager stateManager = await SetupStateWithEvents(10);
        var snapshot = new SnapshotRecord(7, new { State = "at-seq-7" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        var reader = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert - should have snapshot state + only tail events (8, 9, 10)
        _ = result.ShouldNotBeNull();
        result.UsedSnapshot.ShouldBeTrue();
        _ = result.SnapshotState.ShouldNotBeNull();
        result.Events.Count.ShouldBe(3);
        result.Events[0].SequenceNumber.ShouldBe(8);
        result.Events[1].SequenceNumber.ShouldBe(9);
        result.Events[2].SequenceNumber.ShouldBe(10);
        result.LastSnapshotSequence.ShouldBe(7);
        result.CurrentSequence.ShouldBe(10);
    }

    // --- 8.2: lastSnapshotSequence correctly flows from rehydration to snapshot creation decision ---

    [Fact]
    public async Task RehydrateWithSnapshot_LastSnapshotSequence_FlowsCorrectlyForSnapshotDecision() {
        // Arrange - snapshot at seq 50, events up to 55
        InMemoryStateManager stateManager = await SetupStateWithEvents(55);
        var snapshot = new SnapshotRecord(50, new { State = "at-50" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        var reader = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        // Act
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert - lastSnapshotSequence=50 is the value AggregateActor Step 5b passes to
        // ShouldCreateSnapshotAsync(domain, newSequence, lastSnapshotSequence=50)
        _ = result.ShouldNotBeNull();
        result.LastSnapshotSequence.ShouldBe(50);
        result.CurrentSequence.ShouldBe(55);
        result.TailEventCount.ShouldBe(5);

        // The delta determines whether a new snapshot is due (default interval=100)
        long deltaFromLastSnapshot = result.CurrentSequence - result.LastSnapshotSequence;
        deltaFromLastSnapshot.ShouldBe(5);
    }

    // --- 8.3: State correctness: snapshot+tail result's tail events match the corresponding events from full-replay ---

    [Fact]
    public async Task SnapshotPlusTail_TailEventsMatch_FullReplayEvents() {
        // Arrange - same state, both paths read from same InMemoryStateManager
        InMemoryStateManager stateManager = await SetupStateWithEvents(10);
        var snapshot = new SnapshotRecord(5, new { State = "at-5" }, DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        var reader = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        // Act - full replay (no snapshot)
        RehydrationResult? fullReplay = await reader.RehydrateAsync(TestIdentity);

        // Act - snapshot + tail
        RehydrationResult? snapshotPlusTail = await reader.RehydrateAsync(TestIdentity, snapshot);

        // Assert - full replay has all 10 events, no snapshot state
        _ = fullReplay.ShouldNotBeNull();
        fullReplay.Events.Count.ShouldBe(10);
        fullReplay.UsedSnapshot.ShouldBeFalse();
        fullReplay.LastSnapshotSequence.ShouldBe(0);

        // Assert - snapshot+tail has snapshot state + tail events 6-10
        _ = snapshotPlusTail.ShouldNotBeNull();
        snapshotPlusTail.Events.Count.ShouldBe(5);
        snapshotPlusTail.UsedSnapshot.ShouldBeTrue();
        snapshotPlusTail.LastSnapshotSequence.ShouldBe(5);

        // Verify: snapshot tail events are identical to the corresponding events from full replay
        for (int i = 0; i < snapshotPlusTail.Events.Count; i++) {
            EventEnvelope tailEvent = snapshotPlusTail.Events[i];
            EventEnvelope fullReplayEvent = fullReplay.Events[i + 5]; // offset by snapshot sequence

            tailEvent.SequenceNumber.ShouldBe(fullReplayEvent.SequenceNumber);
            tailEvent.AggregateId.ShouldBe(fullReplayEvent.AggregateId);
            tailEvent.TenantId.ShouldBe(fullReplayEvent.TenantId);
            tailEvent.EventTypeName.ShouldBe(fullReplayEvent.EventTypeName);
            tailEvent.CorrelationId.ShouldBe(fullReplayEvent.CorrelationId);
        }

        // Both should report the same current sequence
        fullReplay.CurrentSequence.ShouldBe(snapshotPlusTail.CurrentSequence);
    }

    // --- 8.4: No-snapshot fallback works correctly for new aggregates ---

    [Fact]
    public async Task NewAggregate_NoSnapshot_ReturnsNull() {
        // Arrange - empty state (new aggregate, no events, no metadata)
        var stateManager = new InMemoryStateManager();
        var reader = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        // Act - no snapshot for new aggregate
        RehydrationResult? result = await reader.RehydrateAsync(TestIdentity);

        // Assert - null indicates new aggregate, caller should treat as brand new
        result.ShouldBeNull();
    }
}
