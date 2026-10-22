using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainPartitionedSharedProjectionRebuildTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalNamedAbortRefusesUnavailableOrCorruptOriginalOwnerInventoryAndPreservesOpen(bool corrupt)
    {
        var store = new InMemoryReadModelStore();
        var handler = new PartitionedHandler { FoldOrdinaryRows = true };
        var scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("unavailable-original-retirement");
        var owner = ("owner-a", Event([1]) with { GlobalPosition = 1 });
        _ = await BeginReviewFiveAsync(provider, identity, [owner]);
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        byte[]? original = null;
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.AfterInstallOperation && ordinal == 0 && original is null)
                original = store.SnapshotRaw(StoreName, scratch);
            return Task.CompletedTask;
        };
        (await AccumulateAsync(provider, identity, 0, owner)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.BatchFaultHook = null;
        var abort = Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort);
        (await RebuildAsync(provider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string inventoryKey = DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0) + ":retired";
        var inventory = await store.GetAsync<DomainSharedProjectionScratchRetirement>(StoreName, inventoryKey);
        if (corrupt) await store.SaveAsync(StoreName, inventoryKey, inventory.Value! with { ChunkCount = -1 });
        else (await store.TryEraseAsync(StoreName, inventoryKey, inventory.ETag!)).ShouldBeTrue();
        store.SeedRaw(StoreName, scratch, JsonNode.Parse(original!)!);
        byte[] open = store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash()))!;
        byte[] terminal = store.SnapshotRaw(StoreName, session)!;
        using var replaced = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            (await RebuildAsync(replaced, abort)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
            store.SnapshotRaw(StoreName, scratch).ShouldBe(original);
            store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash())).ShouldBe(open);
            store.SnapshotRaw(StoreName, session).ShouldBe(terminal);
        }
        await store.SaveAsync(StoreName, inventoryKey, inventory.Value!);
        (await RebuildAsync(replaced, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        store.SnapshotRaw(StoreName, scratch).ShouldBeNull();
        store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash())).ShouldBe(open);
    }

    [Fact]
    public async Task TerminalAcceptedStreamedOwnerReconcilesLateFragmentPartAndHashPageAndAllowsFreshNamedGrowth()
    {
        var store = new InMemoryReadModelStore();
        var handler = new StreamedPartitionedHandler { FoldOrdinaryRows = true, FailBufferedOwnerFold = true };
        var scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("late-streamed-transport");
        ProjectionEventDto item = Event(RandomNumberGenerator.GetBytes(1_048_576)) with { GlobalPosition = 1 };
        _ = await BeginReviewFiveAsync(provider, identity, [("owner-a", item)]);
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string fragment = session + ":transport:0:0:fragment:0";
        string part = session + ":transport:0:0";
        string hashPage = DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(session, 0, 0);
        var captured = new Dictionary<string, byte[]>();
        store.BatchFaultHook = (phase, ordinal, _) =>
        {
            if (phase == ReadModelBatchPhase.AfterInstallOperation && ordinal == 0)
                foreach (string key in new[] { fragment, part, hashPage })
                    if (!captured.ContainsKey(key) && store.SnapshotRaw(StoreName, key) is { } bytes
                        && JsonNode.Parse(bytes)?["$hxrmb"] is not null) captured.Add(key, bytes);
            return Task.CompletedTask;
        };
        (await RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: [item], PartOrdinal: 0))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 1,
            ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [item])))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        store.BatchFaultHook = null;
        captured.Count.ShouldBe(3);
        var abort = Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort);
        (await RebuildAsync(provider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var terminal = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, session)).Value!;
        terminal.ScratchCleanedOwnerCount.ShouldBe(1); terminal.ScratchIndexesCleaned.ShouldBeTrue();
        var inventory = (await store.GetAsync<DomainSharedProjectionScratchRetirement>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0) + ":retired")).Value!;
        inventory.TransportPartCount.ShouldBe(1); inventory.ChunkCount.ShouldBe(5);
        byte[] open = store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash()))!;
        foreach (var row in captured) store.SeedRaw(StoreName, row.Key, JsonNode.Parse(row.Value)!);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var replaced = Provider(store, store, coordinator, handler);
        (await RebuildAsync(replaced, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        foreach (string key in captured.Keys) store.SnapshotRaw(StoreName, key).ShouldBeNull();
        store.SnapshotRaw(StoreName, ("shared-epoch:" + scope.ComputeHash())).ShouldBe(open);
        (await RebuildAsync(replaced, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionEventDto growth = Event([9]) with { GlobalPosition = 2, MessageId = "after-late-transport" };
        var delivered = await DomainProjectionDispatcher.DispatchAsync(replaced,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = Identity("fresh-after-late-transport");
        _ = await BeginReviewFiveAsync(replaced, fresh, [("owner-a", growth)]);
        handler.FailBufferedOwnerFold = false;
        var accepted = await AccumulateAsync(replaced, fresh, 0, ("owner-a", growth));
        (await FinishAsync(replaced, fresh, accepted)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldNotBeNull();
    }
    [Fact]
    public async Task NamedMultiOwnerAccumulateRetriesRetainedFenceConflictAndKeepsOrdinaryGrowthAndFreshRebuild()
    {
        var store = new InMemoryReadModelStore();
        var handler = new PartitionedHandler { FoldOrdinaryRows = true };
        var scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("competing-retained-owners");
        var first = ("owner-a", Event([1]) with { GlobalPosition = 1 });
        var second = ("owner-b", Event([2]) with { GlobalPosition = 2 });
        _ = await BeginReviewFiveAsync(provider, identity, [first, second]);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(DomainSharedProjectionRebuildSessionKey.Compute(identity), 0, 0);
        int conflicts = 0;
        bool competing = false;
        store.BatchFaultHook = async (phase, ordinal, token) =>
        {
            if (phase != ReadModelBatchPhase.AfterInstallOperation || ordinal != 0 || competing || conflicts >= 2
                || store.SnapshotRaw(StoreName, scratch) is not { } bytes || JsonNode.Parse(bytes)?["$hxrmb"] is null) return;
            int competitor = conflicts++;
            competing = true;
            (await new SharedProjectionEpochCoordinator(store, store).TryWriteScratchAsync(scope, identity.OperationId,
                "competing-private-" + competitor, new { value = competitor }, "competing-batch-" + competitor,
                ReadModelBatchConcurrency.CreateOnly, token)).ShouldBeTrue();
            competing = false;
        };
        DomainSharedProjectionRebuildResponse accepted = await AccumulateAsync(provider, identity, 0, first);
        for (int retry = 0; accepted.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) && retry < 5; retry++)
            accepted = await AccumulateAsync(provider, identity, 0, first);
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        conflicts.ShouldBe(2); store.BatchFaultHook = null;
        (await AccumulateAsync(provider, identity, 1, second)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var abort = Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort);
        (await RebuildAsync(provider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        ProjectionEventDto growth = Event([9]) with { GlobalPosition = 3, MessageId = "after-competing-owners" };
        using var replaced = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        var delivered = await DomainProjectionDispatcher.DispatchAsync(replaced,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = Identity("fresh-after-competing-owners");
        _ = await BeginReviewFiveAsync(replaced, fresh, [("owner-a", growth)]);
        var current = await AccumulateAsync(replaced, fresh, 0, ("owner-a", growth));
        (await FinishAsync(replaced, fresh, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }

    private static Task<DomainSharedProjectionRebuildResponse> BeginReviewFiveAsync(IServiceProvider provider,
        DomainSharedProjectionRebuildIdentity identity, IReadOnlyList<(string Owner, ProjectionEventDto Event)> histories)
    {
        string inventory = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int ordinal = 0; ordinal < histories.Count; ordinal++)
            inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(inventory, ordinal,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(histories[ordinal].Owner, false, [histories[ordinal].Event]));
        return RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: histories.ToDictionary(item => item.Owner, item => item.Event.GlobalPosition)));
    }

}
