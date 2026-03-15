
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Integration tests for snapshot creation within the event persistence pipeline.
/// Uses InMemoryStateManager for real actor state behavior (pending/committed semantics).
/// </summary>
public class SnapshotCreationIntegrationTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static (SnapshotManager SnapshotManager, InMemoryStateManager StateManager) CreateComponents(
        int defaultInterval = 100) {
        IOptions<SnapshotOptions> options = Options.Create(new SnapshotOptions { DefaultInterval = defaultInterval });
        ILogger<SnapshotManager> logger = Substitute.For<ILogger<SnapshotManager>>();
        var stateManager = new InMemoryStateManager();
        return (new SnapshotManager(options, logger, new NoOpEventPayloadProtectionService()), stateManager);
    }

    /// <summary>
    /// Simulates persisting N events for an aggregate using InMemoryStateManager.
    /// Returns the new sequence number.
    /// </summary>
    private static async Task<long> PersistEventsAsync(
        InMemoryStateManager stateManager,
        AggregateIdentity identity,
        int eventCount,
        long startingSequence = 0) {
        long currentSequence = startingSequence;

        for (int i = 0; i < eventCount; i++) {
            currentSequence++;
            string key = $"{identity.EventStreamKeyPrefix}{currentSequence}";
            var envelope = new EventEnvelope(
                identity.AggregateId,
                identity.TenantId,
                identity.Domain,
                currentSequence,
                DateTimeOffset.UtcNow,
                $"corr-{currentSequence}",
                $"cause-{currentSequence}",
                "user-1",
                "1.0.0",
                "TestEvent",
                "json",
                [1, 2, 3],
                null);

            await stateManager.SetStateAsync(key, envelope).ConfigureAwait(false);
        }

        await stateManager
            .SetStateAsync(identity.MetadataKey, new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null))
            .ConfigureAwait(false);

        return currentSequence;
    }

    // === 10.2: Process 100 events, verify snapshot created at sequence 100 ===

    [Fact]
    public async Task Process100Events_SnapshotCreatedAtSequence100() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 100);

        // Act: persist 100 events and check snapshot
        long newSequence = await PersistEventsAsync(stateManager, TestIdentity, 100);
        var state = new { EventCount = 100 };

        bool shouldSnapshot = await snapshotManager.ShouldCreateSnapshotAsync("test-domain", newSequence, 0);
        shouldSnapshot.ShouldBeTrue();

        await snapshotManager.CreateSnapshotAsync(TestIdentity, newSequence, state, stateManager);
        await stateManager.SaveStateAsync();

        // Assert: snapshot exists in committed state
        ConditionalValue<SnapshotRecord> result = await stateManager
            .TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey);
        result.HasValue.ShouldBeTrue();
        result.Value.SequenceNumber.ShouldBe(100);
    }

    // === 10.3: Process 250 events, snapshot at 200 (overwritten, only latest exists) ===

    [Fact]
    public async Task Process250Events_SnapshotOverwrittenAtSubsequentInterval() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 100);

        // Act: persist 100 events → snapshot at 100
        long seq = await PersistEventsAsync(stateManager, TestIdentity, 100);
        var state100 = new { Seq = 100 };

        bool should100 = await snapshotManager.ShouldCreateSnapshotAsync("test-domain", seq, 0);
        should100.ShouldBeTrue();
        await snapshotManager.CreateSnapshotAsync(TestIdentity, seq, state100, stateManager);
        await stateManager.SaveStateAsync();

        // Persist 100 more events (101-200) → snapshot at 200
        seq = await PersistEventsAsync(stateManager, TestIdentity, 100, startingSequence: 100);
        var state200 = new { Seq = 200 };

        bool should200 = await snapshotManager.ShouldCreateSnapshotAsync("test-domain", seq, 100);
        should200.ShouldBeTrue();
        await snapshotManager.CreateSnapshotAsync(TestIdentity, seq, state200, stateManager);
        await stateManager.SaveStateAsync();

        // Persist 50 more events (201-250) → no new snapshot
        seq = await PersistEventsAsync(stateManager, TestIdentity, 50, startingSequence: 200);
        bool should250 = await snapshotManager.ShouldCreateSnapshotAsync("test-domain", seq, 200);
        should250.ShouldBeFalse();
        await stateManager.SaveStateAsync();

        // Assert: only latest snapshot exists at sequence 200
        ConditionalValue<SnapshotRecord> result = await stateManager
            .TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey);
        result.HasValue.ShouldBeTrue();
        result.Value.SequenceNumber.ShouldBe(200);
    }

    // === 10.4: Process 50 events, no snapshot created ===

    [Fact]
    public async Task Process50Events_NoSnapshotCreated() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 100);

        // Act: persist 50 events
        long seq = await PersistEventsAsync(stateManager, TestIdentity, 50);

        bool shouldSnapshot = await snapshotManager.ShouldCreateSnapshotAsync("test-domain", seq, 0);
        shouldSnapshot.ShouldBeFalse();
        await stateManager.SaveStateAsync();

        // Assert: no snapshot
        ConditionalValue<SnapshotRecord> result = await stateManager
            .TryGetStateAsync<SnapshotRecord>(TestIdentity.SnapshotKey);
        result.HasValue.ShouldBeFalse();
    }

    // === 10.5: Snapshot key matches AggregateIdentity.SnapshotKey pattern ===

    [Fact]
    public async Task SnapshotKey_MatchesAggregateIdentityPattern() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 10);
        var identity = new AggregateIdentity("acme", "orders", "order-42");

        // Act
        long seq = await PersistEventsAsync(stateManager, identity, 10);
        await snapshotManager.CreateSnapshotAsync(identity, seq, new { Data = "test" }, stateManager);
        await stateManager.SaveStateAsync();

        // Assert: key = "acme:orders:order-42:snapshot"
        string expectedKey = "acme:orders:order-42:snapshot";
        identity.SnapshotKey.ShouldBe(expectedKey);

        ConditionalValue<SnapshotRecord> result = await stateManager
            .TryGetStateAsync<SnapshotRecord>(expectedKey);
        result.HasValue.ShouldBeTrue();
    }

    // === 10.6: Snapshot creation is atomic with event persistence ===

    [Fact]
    public async Task SnapshotCreation_AtomicWithEventPersistence() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 10);

        // Act: persist 10 events AND snapshot, all before SaveStateAsync
        long seq = await PersistEventsAsync(stateManager, TestIdentity, 10);
        await snapshotManager.CreateSnapshotAsync(TestIdentity, seq, new { Data = "state" }, stateManager);

        // Before SaveStateAsync: nothing committed yet
        stateManager.CommittedState.ShouldNotContainKey(TestIdentity.SnapshotKey);
        stateManager.CommittedState.ShouldNotContainKey($"{TestIdentity.EventStreamKeyPrefix}1");

        // After SaveStateAsync: both events AND snapshot committed atomically
        await stateManager.SaveStateAsync();

        stateManager.CommittedState.ShouldContainKey(TestIdentity.SnapshotKey);
        stateManager.CommittedState.ShouldContainKey($"{TestIdentity.EventStreamKeyPrefix}1");
        stateManager.CommittedState.ShouldContainKey($"{TestIdentity.EventStreamKeyPrefix}10");
    }

    // === Additional: LoadSnapshot round-trip with InMemoryStateManager ===

    [Fact]
    public async Task LoadSnapshot_RoundTripsCorrectly() {
        // Arrange
        (SnapshotManager snapshotManager, InMemoryStateManager stateManager) = CreateComponents(defaultInterval: 10);
        var state = new { Counter = 42, Name = "test" };

        // Act: create and commit snapshot
        await snapshotManager.CreateSnapshotAsync(TestIdentity, 10, state, stateManager);
        await stateManager.SaveStateAsync();

        // Load snapshot
        SnapshotRecord? loaded = await snapshotManager.LoadSnapshotAsync(TestIdentity, stateManager);

        // Assert
        _ = loaded.ShouldNotBeNull();
        loaded.SequenceNumber.ShouldBe(10);
        loaded.Domain.ShouldBe("test-domain");
        loaded.AggregateId.ShouldBe("agg-001");
        loaded.TenantId.ShouldBe("test-tenant");
    }
}
