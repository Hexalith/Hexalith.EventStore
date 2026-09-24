using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Proves shared projection promotion and replay over the selected Dapr/Redis state provider.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class SharedProjectionEpochLiveSidecarTests(DaprTestContainerFixture fixture)
{
    [Fact]
    public async Task ConcurrentDeliveryAndRestart_PromoteOneGenerationWithPersistedCatchUp()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient firstClient = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var firstStore = new DaprReadModelStore(firstClient, Options.Create(new ReadModelBatchOptions()));
        var first = new SharedProjectionEpochCoordinator(firstStore, firstStore);
        SharedProjectionLease lease = await first.RegisterWriterAsync(scope, "ordinary-dispatch");
        (await first.JournalAsync(scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await first.CatchUpAsync(scope, Fold(first, scope))).ShouldBe(1);
        (await first.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).Value!.Value.ShouldBe(1);

        (await first.BeginAsync(scope, "rebuild", "inventory-1", new Dictionary<string, long> { ["stream-a"] = 1 }))
            .ShouldBe(1);
        lease = await first.RefreshLeaseAsync(scope, "ordinary-dispatch");
        (await first.JournalAsync(scope, lease, Delivery(2))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await first.CatchUpAsync(scope, Fold(first, scope))).ShouldBe(0);
        string buildingJson = await fixture.GetGenericStateJsonAsync(scope.StateKey);
        buildingJson.ShouldContain("stream-a");
        buildingJson.ShouldContain("\"envelopePosition\":2");

        _ = await first.StageAsync(scope, "rebuild", [ReadModelBatchOperation.Write(
            "index", new SharedProjectionLiveCounter(1), ReadModelBatchConcurrency.LastWrite)]);
        await first.CommitAsync(scope, "rebuild");

        using DaprClient restartedClient = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var restartedStore = new DaprReadModelStore(restartedClient, Options.Create(new ReadModelBatchOptions()));
        var restarted = new SharedProjectionEpochCoordinator(restartedStore, restartedStore);
        SharedProjectionReadResult<SharedProjectionLiveCounter> beforeCatchUp = await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index");
        beforeCatchUp.IsStale.ShouldBeTrue();
        (await restarted.CatchUpAsync(scope, Fold(restarted, scope))).ShouldBe(1);
        SharedProjectionReadResult<SharedProjectionLiveCounter> settled = await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index");
        settled.Generation.ShouldBe(1);
        settled.IsStale.ShouldBeFalse();
        settled.Value!.Value.ShouldBe(2);
        (await restarted.GetCheckpointAsync(scope, "stream-a"))!.Position.ShouldBe(2);
        (await restarted.JournalAsync(scope, lease, Delivery(2))).ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await restarted.JournalAsync(scope, lease, Delivery(2) with { CanonicalPayload = [99] }))
            .ShouldBe(SharedProjectionJournalResult.IdentityConflict);

        string persistedEpoch = await fixture.GetGenericStateJsonAsync(scope.StateKey);
        persistedEpoch.ShouldContain("\"activeGeneration\":1");
        persistedEpoch.ShouldContain("\"journal\":[]");
        (await fixture.GetGenericStateJsonAsync(scope.PhysicalKey(1, "index"))).ShouldContain("2");
    }

    [Fact]
    public async Task ConcurrentCaptureAndDelivery_ResolveRedisCasRaceWithoutLosingAcknowledgedPosition()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient firstClient = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        using DaprClient secondClient = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var firstStore = new DaprReadModelStore(firstClient, Options.Create(new ReadModelBatchOptions()));
        var secondStore = new DaprReadModelStore(secondClient, Options.Create(new ReadModelBatchOptions()));
        var initial = new SharedProjectionEpochCoordinator(firstStore, firstStore);
        SharedProjectionLease lease = await initial.RegisterWriterAsync(scope, "ordinary-dispatch");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var arrived = new SemaphoreSlim(0, 2);
        using var release = new SemaphoreSlim(0, 2);
        var captureStore = new RacingSharedProjectionStore(firstStore, scope.StateKey, arrived, release);
        var deliveryStore = new RacingSharedProjectionStore(secondStore, scope.StateKey, arrived, release);
        var capturing = new SharedProjectionEpochCoordinator(
            captureStore, firstStore);
        var delivering = new SharedProjectionEpochCoordinator(
            deliveryStore, secondStore);

        async Task<bool> TryBeginAsync()
        {
            try
            {
                _ = await capturing.BeginAsync(scope, "racing-rebuild", "inventory", new Dictionary<string, long>(), timeout.Token);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        Task<bool> began = TryBeginAsync();
        Task<SharedProjectionJournalResult> delivered = delivering.JournalAsync(scope, lease, Delivery(1), timeout.Token);
        await arrived.WaitAsync(timeout.Token);
        await arrived.WaitAsync(timeout.Token);
        captureStore.InterceptedEtag.ShouldNotBeNull().ShouldBe(deliveryStore.InterceptedEtag);
        release.Release(2);
        await Task.WhenAll(began, delivered);
        bool captureWon = await began;
        SharedProjectionJournalResult deliveryResult = await delivered;

        var recovered = new SharedProjectionEpochCoordinator(secondStore, secondStore);
        int stagedCount;
        if (captureWon)
        {
            deliveryResult.ShouldBeOneOf(SharedProjectionJournalResult.Backpressure, SharedProjectionJournalResult.StaleLease);
            lease = await recovered.RefreshLeaseAsync(scope, "ordinary-dispatch");
            (await recovered.JournalAsync(scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);
            stagedCount = 0;
        }
        else
        {
            deliveryResult.ShouldBe(SharedProjectionJournalResult.Journaled);
            (await recovered.CatchUpAsync(scope, Fold(recovered, scope))).ShouldBe(1);
            (await recovered.BeginAsync(scope, "racing-rebuild", "inventory", new Dictionary<string, long> {
                ["stream-a"] = 1,
            })).ShouldBe(1);
            stagedCount = 1;
        }

        _ = await recovered.StageAsync(scope, "racing-rebuild", [ReadModelBatchOperation.Write(
            "index", new SharedProjectionLiveCounter(stagedCount), ReadModelBatchConcurrency.LastWrite)]);
        await recovered.CommitAsync(scope, "racing-rebuild");
        _ = await recovered.CatchUpAsync(scope, Fold(recovered, scope));

        using DaprClient persistedClient = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var persistedStore = new DaprReadModelStore(persistedClient, Options.Create(new ReadModelBatchOptions()));
        var restarted = new SharedProjectionEpochCoordinator(persistedStore, persistedStore);
        (await restarted.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(1);
        SharedProjectionReadResult<SharedProjectionLiveCounter> selected = await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index");
        selected.Generation.ShouldBe(1);
        selected.IsStale.ShouldBeFalse();
        selected.Value!.Value.ShouldBe(1);
        (await restarted.GetCheckpointAsync(scope, "stream-a"))!.Position.ShouldBe(1);
        (await restarted.JournalAsync(scope, await restarted.RefreshLeaseAsync(scope, "ordinary-dispatch"), Delivery(1)))
            .ShouldBe(captureWon
                ? SharedProjectionJournalResult.AlreadyJournaled
                : SharedProjectionJournalResult.AlreadyCaptured);
    }

    [Fact]
    public async Task InterruptedCaptureAndCatchUp_ResumeFromRedisBeforeSelectingOrAcknowledging()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var initial = new SharedProjectionEpochCoordinator(store, store);
        _ = await initial.RegisterWriterAsync(scope, "ordinary-dispatch");
        var interruptedCapture = new SharedProjectionEpochCoordinator(
            new FaultingSharedProjectionStore(store, 1, failAfterWrite: true), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => interruptedCapture.BeginAsync(
            scope, "capture-crash", "inventory", new Dictionary<string, long>()));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
        (await restarted.BeginAsync(scope, "capture-crash", "inventory", new Dictionary<string, long>()))
            .ShouldBe(1);
        SharedProjectionLease lease = await restarted.RefreshLeaseAsync(scope, "ordinary-dispatch");
        (await restarted.JournalAsync(scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await restarted.StageAsync(scope, "capture-crash", [ReadModelBatchOperation.Write(
            "index", new SharedProjectionLiveCounter(0), ReadModelBatchConcurrency.LastWrite)]);
        await restarted.CommitAsync(scope, "capture-crash");

        var interruptedCatchUp = new SharedProjectionEpochCoordinator(
            new FaultingSharedProjectionStore(store, 1, failAfterWrite: true), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => interruptedCatchUp.CatchUpAsync(
            scope, Fold(interruptedCatchUp, scope)));
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(1);
        restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.CatchUpAsync(scope, Fold(restarted, scope))).ShouldBe(1);
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(0);
        (await restarted.GetCheckpointAsync(scope, "stream-a"))!.Position.ShouldBe(1);
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).Value!.Value.ShouldBe(1);
    }

    [Fact]
    public async Task InterruptedChunkReservation_RedeliveryCompletesBeforeCheckpointAndPromotion()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var initial = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await initial.RegisterWriterAsync(scope, "ordinary-dispatch");
        var delivery = new SharedProjectionDelivery("stream-a", 1, new byte[100_000], [1]);
        var interrupted = new SharedProjectionEpochCoordinator(new FaultingSharedProjectionStore(store, 3), store);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => interrupted.JournalAsync(scope, lease, delivery));
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(scope)).PendingDeliveryCount.ShouldBe(1);
        (await restarted.GetCheckpointAsync(scope, "stream-a")).ShouldBeNull();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => restarted.CatchUpAsync(scope, Fold(restarted, scope)));
        (await restarted.JournalAsync(scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await restarted.GetCheckpointAsync(scope, "stream-a"))!.Position.ShouldBe(1);
        (await restarted.CatchUpAsync(scope, Fold(restarted, scope))).ShouldBe(1);
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).Value!.Value.ShouldBe(1);
        (await fixture.GetGenericStateJsonAsync(scope.StateKey)).ShouldContain("\"journal\":[]");
    }

    [Fact]
    public async Task InterruptedStage_RestartCanCommitCompleteRedisManifest()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var initial = new SharedProjectionEpochCoordinator(store, store);
        _ = await initial.RegisterWriterAsync(scope, "ordinary-dispatch");
        _ = await initial.BeginAsync(scope, "rebuild", "inventory", new Dictionary<string, long>());
        ReadModelBatchOperation[] manifest = [.. Enumerable.Range(0, 300).Select(index =>
            ReadModelBatchOperation.Write(
                "item-" + index.ToString("D5"),
                new SharedProjectionLiveCounter(index),
                ReadModelBatchConcurrency.LastWrite))];
        var interrupted = new SharedProjectionEpochCoordinator(new FaultingSharedProjectionStore(store, 2), store);

        _ = await Should.ThrowAsync<InvalidOperationException>(() => interrupted.StageAsync(scope, "rebuild", manifest));
        (await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey)).Value!
            .StageChunks.ShouldNotBeNull();
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        _ = await restarted.StageAsync(scope, "rebuild", manifest);
        await restarted.CommitAsync(scope, "rebuild");
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "item-00000")).Value!.Value.ShouldBe(0);
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "item-00299")).Value!.Value.ShouldBe(299);
        (await fixture.GetGenericStateJsonAsync(scope.StateKey)).ShouldContain("\"activeGeneration\":1");
    }

    [Fact]
    public async Task CorruptReservedManifestChunk_FailsClosedWithoutSelectingPartialGeneration()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(scope, "corrupt-rebuild", "inventory", new Dictionary<string, long>());
        ReadModelBatchOperation[] manifest = [.. Enumerable.Range(0, 300).Select(index =>
            ReadModelBatchOperation.Write(
                "item-" + index.ToString("D5"),
                new SharedProjectionLiveCounter(index),
                ReadModelBatchConcurrency.LastWrite))];
        var stopped = new SharedProjectionEpochCoordinator(new FaultingSharedProjectionStore(store, 2), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => stopped.StageAsync(scope, "corrupt-rebuild", manifest));
        SharedProjectionChunkReference reserved = (await store.GetAsync<SharedProjectionEpochState>(
            scope.StoreName, scope.StateKey)).Value!.StageChunks.ShouldNotBeNull();
        string chunkKey = SharedProjectionChunkStore.Key(scope, reserved, 0);
        await store.SaveAsync(scope.StoreName, chunkKey,
            new SharedProjectionChunk(reserved.Digest, "BAD-DIGEST", [99], false));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => restarted.StageAsync(scope, "corrupt-rebuild", manifest));
        _ = await Should.ThrowAsync<InvalidOperationException>(() => restarted.CommitAsync(scope, "corrupt-rebuild"));
        (await restarted.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
        (await restarted.ReadGenerationAsync<SharedProjectionLiveCounter>(scope, 1, "item-00000")).Value.ShouldBeNull();
        (await fixture.GetGenericStateJsonAsync(scope.StateKey)).ShouldContain("\"activeGeneration\":0");
    }

    [Fact]
    public async Task InterruptedCommitAbortAndOffboard_ReconcileSelectedRedisGeneration()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var initial = new SharedProjectionEpochCoordinator(store, store);
        _ = await initial.RegisterWriterAsync(scope, "ordinary-dispatch");
        _ = await initial.BeginAsync(scope, "commit-operation", "inventory-0", new Dictionary<string, long>());
        _ = await initial.StageAsync(scope, "commit-operation", [ReadModelBatchOperation.Write(
            "index", new SharedProjectionLiveCounter(5), ReadModelBatchConcurrency.LastWrite)]);

        var interruptedCommit = new SharedProjectionEpochCoordinator(
            new FaultingSharedProjectionStore(store, 1, failAfterWrite: true), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => interruptedCommit.CommitAsync(scope, "commit-operation"));
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.CommitAsync(scope, "commit-operation");
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).Value!.Value.ShouldBe(5);
        (await restarted.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(1);

        _ = await restarted.BeginAsync(scope, "abort-operation", "inventory-1", new Dictionary<string, long>());
        _ = await restarted.StageAsync(scope, "abort-operation", [ReadModelBatchOperation.Write(
            "index", new SharedProjectionLiveCounter(99), ReadModelBatchConcurrency.LastWrite)]);
        var interruptedAbort = new SharedProjectionEpochCoordinator(
            new FaultingSharedProjectionStore(store, 1, failAfterWrite: true), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => interruptedAbort.AbortAsync(
            scope, "abort-operation", Fold(interruptedAbort, scope)));
        restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.AbortAsync(scope, "abort-operation", Fold(restarted, scope));
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).Value!.Value.ShouldBe(5);
        (await restarted.ReadGenerationAsync<SharedProjectionLiveCounter>(scope, 2, "index")).Value.ShouldBeNull();

        var interruptedOffboard = new SharedProjectionEpochCoordinator(
            new FaultingSharedProjectionStore(store, 1, failAfterWrite: true), store);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => interruptedOffboard.OffboardTenantAsync(scope, "audit-1"));
        restarted = new SharedProjectionEpochCoordinator(store, store);
        await restarted.OffboardTenantAsync(scope, "audit-1");
        (await restarted.GetStatusAsync(scope)).IsOffboarded.ShouldBeTrue();
        (await restarted.ReadAsync<SharedProjectionLiveCounter>(scope, "index")).IsAvailable.ShouldBeFalse();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => restarted.RegisterWriterAsync(scope, "ordinary-dispatch"));
        (await fixture.GetGenericStateJsonAsync(scope.StateKey)).ShouldContain("\"offboardingAuditId\":\"audit-1\"");
    }

    [Fact]
    public async Task TenThousandCapturedStagedAndDeliveredMembersRemainExactInRedis()
    {
        fixture.ThrowIfHostStopped();
        string tenant = "r4-" + Guid.NewGuid().ToString("N");
        var scope = new SharedProjectionScope("statestore", tenant, "widget", "widget-index", ["ordinary-dispatch"]);
        using DaprClient client = new DaprClientBuilder().UseGrpcEndpoint(fixture.DaprGrpcEndpoint).Build();
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions()));
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "ordinary-dispatch");
        Dictionary<string, long> capture = Enumerable.Range(0, 10_000).ToDictionary(
            index => "stream-" + index.ToString("D5"), _ => 0L, StringComparer.Ordinal);
        _ = await coordinator.BeginAsync(scope, "large-rebuild", "inventory-10000", capture);
        ReadModelBatchOperation[] manifest = [.. Enumerable.Range(0, 10_000).Select(index =>
            ReadModelBatchOperation.Write(
                "item-" + index.ToString("D5"),
                new SharedProjectionLiveCounter(index),
                ReadModelBatchConcurrency.LastWrite))];
        _ = await coordinator.StageAsync(scope, "large-rebuild", manifest);
        await coordinator.CommitAsync(scope, "large-rebuild");

        SharedProjectionEpochState epoch = (await store.GetAsync<SharedProjectionEpochState>(
            scope.StoreName, scope.StateKey)).Value!;
        byte[] capturedBytes = await new SharedProjectionChunkStore(store).ReadAsync(
            scope, epoch.CaptureChunks!, CancellationToken.None);
        JsonSerializer.Deserialize<Dictionary<string, long>>(capturedBytes)!.Count.ShouldBe(10_000);
        (await coordinator.ReadAsync<SharedProjectionLiveCounter>(scope, "item-00000")).Value!.Value.ShouldBe(0);
        (await coordinator.ReadAsync<SharedProjectionLiveCounter>(scope, "item-09999")).Value!.Value.ShouldBe(9999);

        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, "ordinary-dispatch");
        for (int start = 1; start <= 10_000; start += 64)
        {
            int end = Math.Min(10_000, start + 63);
            for (int position = start; position <= end; position++)
            {
                var delivery = new SharedProjectionDelivery("delivery-stream", position, [1], BitConverter.GetBytes(position));
                (await coordinator.JournalAsync(scope, lease, delivery)).ShouldBe(SharedProjectionJournalResult.Journaled);
            }

            (await coordinator.CatchUpAsync(scope, (_, _, _) =>
                Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]))).ShouldBe(end - start + 1);
        }

        (await coordinator.GetCheckpointAsync(scope, "delivery-stream"))!.Position.ShouldBe(10_000);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(
            "localhost:6379,abortConnect=false,allowAdmin=true");
        IDatabase database = redis.GetDatabase();
        string prefix = fixture.AppId + "||";
        RedisResult[] physicalKeys = (RedisResult[])(await database.ExecuteAsync(
            "KEYS", prefix + "shared-generation:" + scope.ComputeHash() + ":1:*"))!;
        RedisResult[] receiptKeys = (RedisResult[])(await database.ExecuteAsync(
            "KEYS", prefix + "shared-receipt:" + scope.ComputeHash() + ":1:*"))!;
        physicalKeys.Length.ShouldBe(10_000);
        receiptKeys.Length.ShouldBe(10_000);
    }

    private static SharedProjectionDelivery Delivery(long position)
        => new("stream-a", position, [(byte)position], BitConverter.GetBytes(position));

    private static Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> Fold(
        SharedProjectionEpochCoordinator coordinator,
        SharedProjectionScope scope)
        => async (_, generation, cancellationToken) =>
        {
            ReadModelEntry<SharedProjectionLiveCounter> prior = await coordinator.ReadGenerationAsync<SharedProjectionLiveCounter>(
                scope, generation, "index", cancellationToken);
            return [ReadModelBatchOperation.Write(
                "index", new SharedProjectionLiveCounter((prior.Value?.Value ?? 0) + 1), ReadModelBatchConcurrency.LastWrite)];
        };

}
