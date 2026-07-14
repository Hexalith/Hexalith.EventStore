using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Story 1.9 Task 10 — Tier 3 live-sidecar persisted proof of the coordinated projection eraser
/// (<see cref="ProjectionEraseCoordinator"/>) against a real <c>daprd</c> sidecar and Redis state store.
/// These tests seed real Redis, run the real erase capabilities (<see cref="DaprReadModelStore"/>,
/// <see cref="ProjectionCheckpointTracker"/>, <see cref="ProjectionRebuildCheckpointStore"/>,
/// <see cref="ProjectionReadModelAddressFactory"/>) over a real <see cref="DaprClient"/>, and assert the
/// persisted Redis end state with fresh reads — a return status is not integration evidence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle gateway is mocked (NSubstitute), by design.</b> The coordinator's only non-Redis
/// collaborator is the internal <see cref="IProjectionLifecycleGateway"/>, which round-trips the
/// fixed-name <c>ProjectionLifecycleActor</c>. The live fixture host registers ONLY
/// <c>AggregateActor</c> (see <see cref="DaprTestContainerFixture"/>), not the lifecycle actor, and a
/// fixed-name actor over the shared sidecar risks the known 60s placement hang. The gateway's
/// admit/record/complete protocol is separately unit-proven in <c>ProjectionLifecycleActorTests</c> and
/// <c>ProjectionEraseCoordinatorTests</c>; mocking it here (Admitted / record=true / complete=true) lets
/// these tests exercise the REAL Redis erase + read-back-classification capabilities without that hang.
/// </para>
/// <para>
/// <b>No state transaction is used (AC8, resumable-not-atomic).</b> The coordinator takes no
/// <see cref="DaprClient"/> and drives each target through single-key first-write-wins
/// <c>TryDeleteStateAsync</c> calls on the eraser/store abstractions; there is no
/// <c>ExecuteStateTransactionAsync</c> anywhere on the erase path. This is structural (verifiable in the
/// constructor signature), so it is asserted here as a documented invariant rather than a mock check.
/// </para>
/// </remarks>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
[Trait("Tier", "3")]
public class ProjectionEraseLiveSidecarTests {
    private const string StoreName = "statestore";
    private const string Slot = "primary";

    private readonly DaprTestContainerFixture _fixture;

    public ProjectionEraseLiveSidecarTests(DaprTestContainerFixture fixture) => _fixture = fixture;

    /// <summary>Canonical, aggregate-owned read-model document seeded and erased through the coordinator.</summary>
    public sealed record ReadModelDoc(int Version);

    /// <summary>Shape-only probe for the projection-checkpoint migration marker (<c>{ "migrated": true }</c>).</summary>
    public sealed record MigrationMarkerProbe(bool Migrated);

    [Fact]
    [Trait("Tier", "3")]
    public async Task CoordinatedErase_PersistsAbsentTargetsAndRetainsMigrationMarker_ResumableNotAtomic() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();

        string unique = Guid.NewGuid().ToString("N");
        (string tenant, string domain, string agg, string projection, string operationId) = FreshScope(unique);
        var identity = new AggregateIdentity(tenant, domain, agg);

        var registry = new ProjectionSlotRegistry();
        registry.Register(projection, Slot, ProjectionReadModelSlotKind.AggregateOwned);
        registry.RegisterCanonicalWriter(domain, projection, Slot);
        EraseHarness harness = EraseHarness.Build(client, registry);

        // Canonical, platform-derived target keys (never caller-supplied).
        string readModelKey = harness.Factory.Create(identity, projection, Slot).Key;
        string deliveryKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, projection);
        string markerKey = ProjectionCheckpointTracker.GetMigratedMarkerKey(identity, projection);
        var rebuildScope = new ProjectionRebuildCheckpointScope(tenant, domain, projection, agg, operationId);
        string rebuildKey = ProjectionRebuildCheckpointStore.GetStateKey(rebuildScope);

        // Seed real Redis.
        // (a) canonical read-model value.
        await client.SaveStateAsync(StoreName, readModelKey, new ReadModelDoc(7));
        // (b) HIGH projection-scoped delivery checkpoint (LastDeliveredSequence=999) via the real write
        //     path, which also persists the migration marker exactly as production reaches this state.
        (await harness.Tracker.SaveDeliveredSequenceAsync(identity, projection, 999)).ShouldBeTrue();
        // (c) aggregate-specific rebuild row (terminal status: does NOT enter the active-rebuild index,
        //     so the erase's active-rebuild gate sees no operator rebuild in flight).
        await client.SaveStateAsync(
            StoreName,
            rebuildKey,
            new ProjectionRebuildCheckpoint(tenant, domain, projection, agg, OperationId: null, 5, ProjectionRebuildStatus.Succeeded, DateTimeOffset.UtcNow, FailureReasonCode: null));

        // Confirm the pre-state is actually present in Redis before erasing.
        (await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKey)).Item1.ShouldNotBeNull();
        (await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, deliveryKey)).Item1!.LastDeliveredSequence.ShouldBe(999);
        (await client.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(StoreName, rebuildKey)).Item1.ShouldNotBeNull();

        ProjectionEraseResult result = await harness.Coordinator.EraseAsync(
            new ProjectionEraseRequest(tenant, domain, agg, projection, [Slot], operationId));

        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);
        result.TargetOutcomes.Select(o => o.TargetKey).ShouldBe([readModelKey, rebuildKey, deliveryKey]);
        result.TargetOutcomes.ShouldAllBe(o => o.Outcome == "Complete");

        // Fresh persisted end-state reads: every erasable target is ABSENT and the marker is RETAINED.
        (await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKey)).Item1.ShouldBeNull("read-model key must be erased");
        (await harness.ReadModelStore.TryReadEtagAsync(StoreName, readModelKey)).Present.ShouldBeFalse("read-model key must not be visible after erase");
        (await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, deliveryKey)).Item1.ShouldBeNull("projection-scoped delivery checkpoint must be erased");
        (await client.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(StoreName, rebuildKey)).Item1.ShouldBeNull("aggregate rebuild row must be erased");

        MigrationMarkerProbe? marker = (await client.GetStateAndETagAsync<MigrationMarkerProbe>(StoreName, markerKey)).Item1;
        marker.ShouldNotBeNull("the migration marker must remain so a later read cannot re-migrate the legacy value");
        marker!.Migrated.ShouldBeTrue();
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task StaleCheckpointRecovery_ReadReturnsZeroFromMarker_WithoutTouchingEventStream() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();

        string unique = Guid.NewGuid().ToString("N");
        (string tenant, string domain, string agg, string projection, string operationId) = FreshScope(unique);
        var identity = new AggregateIdentity(tenant, domain, agg);

        var registry = new ProjectionSlotRegistry();
        registry.Register(projection, Slot, ProjectionReadModelSlotKind.AggregateOwned);
        registry.RegisterCanonicalWriter(domain, projection, Slot);
        EraseHarness harness = EraseHarness.Build(client, registry);

        string readModelKey = harness.Factory.Create(identity, projection, Slot).Key;
        var rebuildScope = new ProjectionRebuildCheckpointScope(tenant, domain, projection, agg, operationId);
        string rebuildKey = ProjectionRebuildCheckpointStore.GetStateKey(rebuildScope);

        // Seed a HIGH delivery checkpoint (999) + marker, plus read-model and rebuild rows. NO event
        // stream is seeded, and none is created by the erase.
        await client.SaveStateAsync(StoreName, readModelKey, new ReadModelDoc(3));
        (await harness.Tracker.SaveDeliveredSequenceAsync(identity, projection, 999)).ShouldBeTrue();
        await client.SaveStateAsync(
            StoreName,
            rebuildKey,
            new ProjectionRebuildCheckpoint(tenant, domain, projection, agg, OperationId: null, 5, ProjectionRebuildStatus.Succeeded, DateTimeOffset.UtcNow, FailureReasonCode: null));

        ProjectionEraseResult result = await harness.Coordinator.EraseAsync(
            new ProjectionEraseRequest(tenant, domain, agg, projection, [Slot], operationId));
        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);

        // AC12: after erase the projection-scoped checkpoint is gone but the marker remains, so a fresh
        // read returns 0 instead of falling back to the legacy 999 high-water mark — the drift gate that
        // previously suppressed sequence-one delivery is gone. The full sequence-one delivery drive is
        // proven separately in ProjectionUpdateOrchestratorTests (asserting no CheckpointDriftDetected);
        // it is deliberately NOT re-driven here because that path is not reliably drivable over the
        // shared sidecar without the fixed-name-actor hang risk, and faking it would be dishonest.
        (await harness.Tracker.ReadDeliveredSequenceAsync(identity, projection)).ShouldBe(0L);

        // The recovery never touches any {tenant}:{domain}:{agg} event-stream key: none was seeded and the
        // aggregate id is globally unique, so a Redis scan for its event-stream keys finds nothing.
        await using ConnectionMultiplexer redis = await ConnectRedisAsync();
        var eventKeys = (RedisResult[])(await redis.GetDatabase().ExecuteAsync("KEYS", $"*{agg}*events*"))!;
        eventKeys.Length.ShouldBe(0, "the coordinated erase must not create or touch any event-stream key");
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task CoordinatedErase_TenantIsolation_LeavesOtherTenantByteForByteUnchanged() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();

        string unique = Guid.NewGuid().ToString("N");
        // Distinct unique tenants; a shared domain/aggregate/projection isolates the tenant segment as the
        // sole difference, proving the erase is tenant-scoped by the key it derives from tenant A alone.
        string tenantA = $"ta{unique}";
        string tenantB = $"tb{unique}";
        string domain = $"d{unique}";
        string agg = $"a{unique}";
        string projection = $"p{unique}";
        string operationId = $"op{unique}";
        var identityA = new AggregateIdentity(tenantA, domain, agg);
        var identityB = new AggregateIdentity(tenantB, domain, agg);

        var registry = new ProjectionSlotRegistry();
        registry.Register(projection, Slot, ProjectionReadModelSlotKind.AggregateOwned);
        registry.RegisterCanonicalWriter(domain, projection, Slot);
        EraseHarness harness = EraseHarness.Build(client, registry);

        string readModelKeyA = harness.Factory.Create(identityA, projection, Slot).Key;
        string readModelKeyB = harness.Factory.Create(identityB, projection, Slot).Key;
        string deliveryKeyB = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identityB, projection);

        // Seed both tenants: read-model + delivery checkpoint each.
        await client.SaveStateAsync(StoreName, readModelKeyA, new ReadModelDoc(1));
        (await harness.Tracker.SaveDeliveredSequenceAsync(identityA, projection, 111)).ShouldBeTrue();
        await client.SaveStateAsync(StoreName, readModelKeyB, new ReadModelDoc(2));
        (await harness.Tracker.SaveDeliveredSequenceAsync(identityB, projection, 555)).ShouldBeTrue();

        // Capture tenant B's exact persisted value + ETag before erasing tenant A.
        (ReadModelDoc? bReadModelBefore, string bReadModelEtagBefore) = await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKeyB);
        (ProjectionCheckpoint? bCheckpointBefore, string bCheckpointEtagBefore) = await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, deliveryKeyB);
        bReadModelBefore.ShouldNotBeNull();
        bCheckpointBefore.ShouldNotBeNull();

        // Erase tenant A only.
        ProjectionEraseResult result = await harness.Coordinator.EraseAsync(
            new ProjectionEraseRequest(tenantA, domain, agg, projection, [Slot], operationId));
        result.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);

        // Tenant A's keys are gone.
        (await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKeyA)).Item1.ShouldBeNull();
        (await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, ProjectionCheckpointTracker.GetProjectionScopedStateKey(identityA, projection))).Item1.ShouldBeNull();

        // Tenant B is byte-for-byte unchanged (value AND ETag). The coordinator derives every target key
        // from tenant A's identity, so tenant B is structurally never read or disclosed.
        (ReadModelDoc? bReadModelAfter, string bReadModelEtagAfter) = await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKeyB);
        (ProjectionCheckpoint? bCheckpointAfter, string bCheckpointEtagAfter) = await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, deliveryKeyB);
        bReadModelAfter.ShouldBe(bReadModelBefore);
        bReadModelEtagAfter.ShouldBe(bReadModelEtagBefore);
        bCheckpointAfter.ShouldBe(bCheckpointBefore);
        bCheckpointEtagAfter.ShouldBe(bCheckpointEtagBefore);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task CoordinatedErase_IdempotentRerun_ConvergesToCompleteWithRedisUnchanged() {
        _fixture.ThrowIfHostStopped();
        using DaprClient client = CreateClient();

        string unique = Guid.NewGuid().ToString("N");
        (string tenant, string domain, string agg, string projection, string operationId) = FreshScope(unique);
        var identity = new AggregateIdentity(tenant, domain, agg);

        var registry = new ProjectionSlotRegistry();
        registry.Register(projection, Slot, ProjectionReadModelSlotKind.AggregateOwned);
        registry.RegisterCanonicalWriter(domain, projection, Slot);
        EraseHarness harness = EraseHarness.Build(client, registry);

        string readModelKey = harness.Factory.Create(identity, projection, Slot).Key;
        string deliveryKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, projection);
        var rebuildScope = new ProjectionRebuildCheckpointScope(tenant, domain, projection, agg, operationId);
        string rebuildKey = ProjectionRebuildCheckpointStore.GetStateKey(rebuildScope);

        await client.SaveStateAsync(StoreName, readModelKey, new ReadModelDoc(4));
        (await harness.Tracker.SaveDeliveredSequenceAsync(identity, projection, 999)).ShouldBeTrue();
        await client.SaveStateAsync(
            StoreName,
            rebuildKey,
            new ProjectionRebuildCheckpoint(tenant, domain, projection, agg, OperationId: null, 5, ProjectionRebuildStatus.Succeeded, DateTimeOffset.UtcNow, FailureReasonCode: null));

        var request = new ProjectionEraseRequest(tenant, domain, agg, projection, [Slot], operationId);

        ProjectionEraseResult first = await harness.Coordinator.EraseAsync(request);
        first.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);

        // Re-run the same request: every target is already absent, so each classifies as an idempotent
        // Complete and the operation converges to Success without any injected partial failure.
        ProjectionEraseResult second = await harness.Coordinator.EraseAsync(request);
        second.Kind.ShouldBe(ProjectionEraseOutcomeKind.Success);
        second.TargetOutcomes.Select(o => o.TargetKey).ShouldBe([readModelKey, rebuildKey, deliveryKey]);
        second.TargetOutcomes.ShouldAllBe(o => o.Outcome == "Complete");

        // Redis is still at the erased end-state after the second run.
        (await client.GetStateAndETagAsync<ReadModelDoc>(StoreName, readModelKey)).Item1.ShouldBeNull();
        (await client.GetStateAndETagAsync<ProjectionCheckpoint>(StoreName, deliveryKey)).Item1.ShouldBeNull();
        (await client.GetStateAndETagAsync<ProjectionRebuildCheckpoint>(StoreName, rebuildKey)).Item1.ShouldBeNull();
    }

    private DaprClient CreateClient() =>
        new DaprClientBuilder().UseGrpcEndpoint(_fixture.DaprGrpcEndpoint).Build();

    private static async Task<ConnectionMultiplexer> ConnectRedisAsync() =>
        await ConnectionMultiplexer.ConnectAsync("localhost:6379,abortConnect=false,allowAdmin=true");

    // Distinct, reserved-char-free, regex-valid identity segments for one run so parallel/repeat runs
    // never collide.
    private static (string Tenant, string Domain, string Aggregate, string Projection, string OperationId) FreshScope(string unique) =>
        ($"t{unique}", $"d{unique}", $"a{unique}", $"p{unique}", $"op{unique}");

    /// <summary>
    /// Bundles the real Dapr-backed erase capabilities and the internal coordinator wired with a mocked
    /// <see cref="IProjectionLifecycleGateway"/>. All stores share one <see cref="DaprClient"/> and one
    /// <see cref="ProjectionOptions"/> pinned to the live <c>statestore</c> component.
    /// </summary>
    private sealed record EraseHarness(
        DaprReadModelStore ReadModelStore,
        ProjectionCheckpointTracker Tracker,
        ProjectionRebuildCheckpointStore RebuildStore,
        ProjectionReadModelAddressFactory Factory,
        ProjectionEraseCoordinator Coordinator) {
        public static EraseHarness Build(DaprClient client, IProjectionSlotRegistry registry) {
            IOptions<ProjectionOptions> options = Options.Create(new ProjectionOptions {
                CheckpointStateStoreName = StoreName,
                ReadModelStateStoreName = StoreName,
            });
            var readModelStore = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
            var tracker = new ProjectionCheckpointTracker(client, options, NullLogger<ProjectionCheckpointTracker>.Instance);
            var rebuildStore = new ProjectionRebuildCheckpointStore(client, options, NullLogger<ProjectionRebuildCheckpointStore>.Instance);
            var factory = new ProjectionReadModelAddressFactory(registry, options);

            IProjectionLifecycleGateway gateway = Substitute.For<IProjectionLifecycleGateway>();
            _ = gateway
                .BeginEraseAsync(
                    Arg.Any<AggregateIdentity>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<CancellationToken>())
                .Returns(new ProjectionEraseAdmission(ProjectionEraseAdmissionKind.Admitted, new Dictionary<string, string>(StringComparer.Ordinal)));
            _ = gateway
                .RecordTargetOutcomeAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _ = gateway
                .CompleteEraseAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var coordinator = new ProjectionEraseCoordinator(
                factory,
                registry,
                rebuildStore,
                gateway,
                NullLogger<ProjectionEraseCoordinator>.Instance,
                readModelStore,
                rebuildStore,
                tracker);

            return new EraseHarness(readModelStore, tracker, rebuildStore, factory, coordinator);
        }
    }
}
