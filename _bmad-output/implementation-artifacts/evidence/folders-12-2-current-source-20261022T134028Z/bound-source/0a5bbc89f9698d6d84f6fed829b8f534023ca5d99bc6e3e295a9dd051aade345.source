using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0)]
    public async Task ProductionNamedAbortResumesItsOwnInterruptedCleanupWithReplacedProvider(ReadModelBatchPhase phase, int ordinal)
    {
        string tenant = "cleanup-restart-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        SharedProjectionScope scope = handler.CreateScope(tenant);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = NamedProvider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        ProjectionEventDto source = NamedEvent(1);
        DomainSharedProjectionRebuildIdentity identity = NamedIdentity(tenant, "cleanup-interrupted");
        (await NamedBegin(provider, identity, [("owner-a", source)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await NamedAccumulate(provider, identity, "owner-a", source)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fault = new ScratchRecoveryLiveBatchStore(client, store, phase, ordinal, "scratch-cleanup:");
        using ServiceProvider interrupted = NamedProvider(store, fault, new SharedProjectionEpochCoordinator(store, fault), handler);
        (await NamedDispatch(interrupted, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Abort, identity))).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(scope.StoreName, session)).Value!.Phase
            .ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        byte[] pending = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!;
        ReadModelBatchEnvelope.IsEnvelope(pending).ShouldBeTrue();
        using DaprClient replacement = Client();
        var replacedStore = new DaprReadModelStore(replacement);
        coordinator = new SharedProjectionEpochCoordinator(replacedStore, replacedStore);
        using ServiceProvider resumed = NamedProvider(replacedStore, replacedStore, coordinator, handler);
        for (int retry = 0; retry < 2; retry++)
            (await NamedDispatch(resumed, new(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Abort, identity))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0)))).ShouldBeFalse();
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse(); status.IsAborting.ShouldBeFalse(); status.ActiveGeneration.ShouldBe(0);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value.ShouldBeNull();
        ProjectionEventDto growth = NamedEvent(9) with { MessageId = "growth", GlobalPosition = 2 };
        ProjectionDispatchResponse delivered = await DomainProjectionDispatcher.DispatchAsync(resumed,
            new(new(tenant, "widget", "owner-a", [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildIdentity fresh = NamedIdentity(tenant, "fresh");
        (await NamedBegin(resumed, fresh, [("owner-a", growth)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await NamedFinish(resumed, fresh, await NamedAccumulate(resumed, fresh, "owner-a", growth))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine($"cleanup-phase={phase}; original-pending={System.Text.Encoding.UTF8.GetString(pending)}; session=Aborted-before-retry; retry=AlreadyCompleted; private-absent=true; epoch=Open; ordinary-and-fresh-rebuild=true");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CowSelectedInheritedAndAbandonedPromotedPointerRemainTruthfulAfterPhysicalRedisExpiry(int scenario)
    {
        string tenant = "cow-physical-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        DateTimeOffset deadline = clock.GetUtcNow().AddSeconds(30);
        clock.Advance(TimeSpan.FromSeconds(27));
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "cow-expiry", ["writer"]) { CopyOnWrite = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "first", "inventory", new Dictionary<string, long>());
        _ = await coordinator.StageStreamingAsync(scope, "first", Operations(0));
        await coordinator.CommitAsync(scope, "first");
        if (scenario != 0)
        {
            _ = await coordinator.BeginAsync(scope, "next", "inventory2", new Dictionary<string, long>());
            _ = await coordinator.StageStreamingAsync(scope, "next", Operations(scenario));
            if (scenario == 1) await coordinator.CommitAsync(scope, "next");
            else await coordinator.AbortAsync(scope, "next", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        }
        string physical = scope.PhysicalKey(1, "model");
        TimeSpan ttl = (await _redis.GetDatabase().KeyTimeToLiveAsync(Key(physical))).ShouldNotBeNull();
        ttl.ShouldBeGreaterThan(TimeSpan.Zero); ttl.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(3));
        clock.SetUtcNow(deadline.AddTicks(-1));
        (await coordinator.ReadAsync<JsonObject>(scope, "model")).Value!["count"]!.GetValue<int>().ShouldBe(1);
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        (await _redis.GetDatabase().KeyExistsAsync(Key(physical))).ShouldBeFalse();
        // Physical absence before the clock's chosen deadline is not silently accepted.
        await Should.ThrowAsync<SharedProjectionReadUnavailableException>(() => coordinator.ReadAsync<JsonObject>(scope, "model"));
        clock.SetUtcNow(deadline);
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
        SharedProjectionReadResult<JsonObject> expired = await coordinator.ReadAsync<JsonObject>(scope, "model");
        expired.IsAvailable.ShouldBeTrue(); expired.Value.ShouldBeNull();
        TestContext.Current.TestOutputHelper!.WriteLine($"cow-scenario={scenario}; physical-key={physical}; remaining-ttl-ms={ttl.TotalMilliseconds}; redis-physically-absent=true; predeadline-unavailable=true; inclusive-available-absence=true");
        async IAsyncEnumerable<ReadModelBatchOperation> Operations(int kind)
        {
            yield return kind == 0 ? ReadModelBatchOperation.WriteExpiring("model", Value(1), ReadModelBatchConcurrency.LastWrite, deadline)
                : ReadModelBatchOperation.Write(kind == 1 ? "unrelated" : "model", Value(99), ReadModelBatchConcurrency.LastWrite);
            await Task.Yield();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyCompensationEvidenceSurvivesRealDeleteInterruptionAndReplacement(bool afterDelete)
    {
        string tenant = "legacy-compensation-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "legacy-index", ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "legacy", "inventory", new Dictionary<string, long>());
        var fence = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        string scratch = tenant + ":private";
        var batch = new ReadModelBatch(new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family, "legacy-scratch"),
            [ReadModelBatchOperation.Write(scratch, Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, fence.Value!, ReadModelBatchConcurrency.Match(fence.ETag!))]);
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(raw, new(), NullLogger.Instance)
            .ExecuteAsync(batch, new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.BeforeCommit, -1), CancellationToken.None));
        var interrupted = new InterruptedLegacyScratchLiveAccessor(raw, scratch, afterDelete);
        (await ReadModelPendingBatchRecovery.RecoverAsync(interrupted, new(), NullLogger.Instance, batch.Scope,
            scope.StateKey, null, CancellationToken.None)).IsResolved.ShouldBeFalse();
        string evidenceKey = ReadModelBatchKeys.ScratchCompensationKey(batch.Scope.ComputeScopeHash());
        byte[] retained = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(evidenceKey), "data"))!;
        retained.ShouldNotBeNull();
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof((await raw.ReadAsync(batch.Scope.ComputeMarkerKey(), CancellationToken.None)).Value)!;
        marker.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborting); marker.ScratchCompensationKey.ShouldBe(evidenceKey);
        ReadModelBatchEnvelope.IsEnvelope((await raw.ReadAsync(scope.StateKey, CancellationToken.None)).Value.Span).ShouldBeTrue();
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement); coordinator = new SharedProjectionEpochCoordinator(store, store);
        (await store.RecoverPendingAsync(batch.Scope, scope.StateKey)).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(batch.Scope, scope.StateKey)).IsResolved.ShouldBeTrue();
        await coordinator.AbortAsync(scope, "legacy", (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        await coordinator.ReclaimScratchReceiptAsync(scope, batch.Scope.BatchId);
        foreach (string key in new[] { scratch, evidenceKey })
            (await _redis.GetDatabase().KeyExistsAsync(Key(key))).ShouldBeFalse();
        ReadModelBatchMarker.ReadProof((await raw.ReadAsync(batch.Scope.ComputeMarkerKey(), CancellationToken.None)).Value)!.Status.ShouldBe(ReadModelBatchMarkerStatus.Aborted);
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        TestContext.Current.TestOutputHelper!.WriteLine($"legacy-after-delete={afterDelete}; retained-compensation={System.Text.Encoding.UTF8.GetString(retained)}; replaced-client-store-coordinator=true; repeated-settlement=true; private-payload-physically-absent=true;bounded-Aborted-fence-retained=true; epoch=Open");
    }
}
