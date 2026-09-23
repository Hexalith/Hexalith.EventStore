using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public sealed class SharedProjectionControlIndexTests
{
    private const string ControlIndexName = "widget-discovery";

    private static readonly SharedProjectionScope s_scope = new(
        "statestore", "tenant-a", "widget", "widget-index", ["ordinary-dispatch"]);

    [Fact]
    public async Task ConcurrentTenantsAndDuplicateDelivery_PersistOneGlobalMembershipPerTenant()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope other = s_scope with { TenantId = "tenant-b" };
        SharedProjectionLease firstLease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        SharedProjectionLease secondLease = await coordinator.RegisterWriterAsync(other, "ordinary-dispatch");

        using var barrier = new Barrier(2);
        int arrivals = 0;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (Interlocked.Increment(ref arrivals) <= 2)
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            }
        };
        SharedProjectionJournalResult[] results = await Task.WhenAll(
            Task.Run(() => coordinator.JournalAsync(s_scope, firstLease, Delivery(1))),
            Task.Run(() => coordinator.JournalAsync(other, secondLease, Delivery(1))));
        store.ConcurrentWriteBeforeTrySave = null;
        results.ShouldAllBe(result => result == SharedProjectionJournalResult.Journaled);

        ReadModelEntry<SharedProjectionControlIndexState> persisted = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, SharedProjectionControlIndexWriter.StateKey(ControlIndexName));
        persisted.Value.ShouldNotBeNull();
        persisted.Value.TenantIds.ShouldBe(["tenant-a", "tenant-b"]);
        persisted.Value.ScopeReceipts.Count.ShouldBe(2);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldBe(["tenant-a", "tenant-b"]);

        (await coordinator.JournalAsync(s_scope, firstLease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
        (await coordinator.JournalAsync(s_scope, firstLease, Delivery(1) with
        {
            ControlIndexIntent = new SharedProjectionControlIndexIntent("different-discovery"),
        })).ShouldBe(SharedProjectionJournalResult.IdentityConflict);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, "different-discovery"))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task CrashAfterGlobalReceiptBeforeTenantJournal_RetryConvergesWithoutStrandingDiscovery()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (Interlocked.Increment(ref writes) == 2)
            {
                throw new InvalidOperationException("crash before journal CAS");
            }
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.JournalAsync(s_scope, lease, Delivery(1)));
        store.ConcurrentWriteBeforeTrySave = null;
        ReadModelEntry<SharedProjectionControlIndexState> global = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, SharedProjectionControlIndexWriter.StateKey(ControlIndexName));
        global.Value.ShouldNotBeNull();
        global.Value.TenantIds.ShouldBe(["tenant-a"]);
        global.Value.ScopeReceipts.Count.ShouldBe(1);
        ReadModelEntry<SharedProjectionEpochState> epoch = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        epoch.Value!.Journal.ShouldBeEmpty();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.JournalAsync(s_scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);
        (await restarted.ReconcileControlIndexAsync(s_scope)).ShouldBe(1);
        (await restarted.CatchUpAsync(s_scope, Fold())).ShouldBe(1);
        (await restarted.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index"))
            .Value!.IndexName.ShouldBe(ControlIndexName);
        (await store.GetAsync<SharedProjectionDeliveryReceipt>(
            s_scope.StoreName,
            s_scope.ReceiptKey(0, "widget-a", 1))).Value.ShouldNotBeNull();
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        (await restarted.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyJournaled);
    }

    [Fact]
    public async Task CrashDuringPreparedCatchUp_ReconcilesControlReceiptBeforeOpeningGeneration()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1))).ShouldBe(SharedProjectionJournalResult.Journaled);

        bool faulted = false;
        store.BatchFaultHook = (phase, _, _) =>
        {
            if (phase == ReadModelBatchPhase.BeforeCommit && !faulted)
            {
                faulted = true;
                throw new InvalidOperationException("crash during prepared catch-up");
            }

            return Task.CompletedTask;
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.CatchUpAsync(s_scope, Fold()));
        ReadModelEntry<SharedProjectionEpochState> pending = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        pending.Value!.Journal.Length.ShouldBe(1);
        pending.Value.Journal[0].Prepared.ShouldNotBeNull();
        pending.Value.Journal[0].PreparedControlIndexIntent.ShouldNotBeNull();
        (await coordinator.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(1);
        (await coordinator.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index")).IsStale.ShouldBeTrue();

        store.BatchFaultHook = null;
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.ReconcileControlIndexAsync(s_scope)).ShouldBe(1);
        (await restarted.CatchUpAsync(s_scope, Fold())).ShouldBe(1);
        (await restarted.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index"))
            .Value!.IndexName.ShouldBe(ControlIndexName);
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        ReadModelEntry<SharedProjectionControlIndexState> global = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, SharedProjectionControlIndexWriter.StateKey(ControlIndexName));
        global.Value!.TenantIds.ShouldBe(["tenant-a"]);
        global.Value.ScopeReceipts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StagedInventoryWithoutOrdinaryDelivery_RegistersDiscoveryBeforePromotion()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(s_scope, "rebuild", "inventory", new Dictionary<string, long>());
        var intent = new SharedProjectionControlIndexIntent(ControlIndexName);
        IReadOnlyList<ReadModelBatchOperation> manifest =
        [
            ReadModelBatchOperation.Write("index", intent, ReadModelBatchConcurrency.LastWrite),
        ];

        _ = await coordinator.StageAsync(s_scope, "rebuild", manifest, controlIndexIntent: intent);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldBe(["tenant-a"]);
        (await coordinator.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index"))
            .Generation.ShouldBe(0);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.StageAsync(
            s_scope,
            "rebuild",
            manifest,
            controlIndexIntent: new SharedProjectionControlIndexIntent("different-discovery")));

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.ReconcileControlIndexAsync(s_scope)).ShouldBe(1);
        await restarted.CommitAsync(s_scope, "rebuild");
        (await restarted.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index"))
            .Value!.IndexName.ShouldBe(ControlIndexName);
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        ReadModelEntry<SharedProjectionControlIndexState> global = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, SharedProjectionControlIndexWriter.StateKey(ControlIndexName));
        global.Value!.ScopeReceipts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CapturedPosition_CannotPublishUnjournaledDiscoveryIntent()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(
            s_scope,
            "rebuild",
            "inventory",
            new Dictionary<string, long> { ["widget-a"] = 1 });
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(s_scope, "ordinary-dispatch");

        (await coordinator.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Backpressure);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldBeEmpty();

        var intent = new SharedProjectionControlIndexIntent(ControlIndexName);
        _ = await coordinator.StageAsync(
            s_scope,
            "rebuild",
            [ReadModelBatchOperation.Write("index", intent, ReadModelBatchConcurrency.LastWrite)],
            controlIndexIntent: intent);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldBe(["tenant-a"]);
        await coordinator.CommitAsync(s_scope, "rebuild");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.AlreadyCaptured);
    }

    [Fact]
    public async Task RestartWithMissingGlobalReceipt_ReconcilesPreparedJournalBeforeStatus()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        string key = SharedProjectionControlIndexWriter.StateKey(ControlIndexName);
        ReadModelEntry<SharedProjectionControlIndexState> applied = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        applied.Value!.ScopeReceipts.Count.ShouldBe(1);

        // Model a restore of the global control document that lagged the tenant journal snapshot.
        (await store.TryEraseAsync(s_scope.StoreName, key, applied.ETag!)).ShouldBeTrue();
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName)).ShouldBeEmpty();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(1);
        ReadModelEntry<SharedProjectionControlIndexState> restored = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        restored.Value!.TenantIds.ShouldBe(["tenant-a"]);
        restored.Value.ScopeReceipts.Count.ShouldBe(1);
        (await restarted.CatchUpAsync(s_scope, Fold())).ShouldBe(1);
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
    }

    [Fact]
    public async Task RestoreAfterJournalDrain_ReconstructsMembershipFromRetainedTenantIntent()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.CatchUpAsync(s_scope, Fold())).ShouldBe(1);
        ReadModelEntry<SharedProjectionEpochState> durable = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        durable.Value!.Journal.ShouldBeEmpty();
        durable.Value.RetainedControlIndexNames.ShouldBe([ControlIndexName]);

        string key = SharedProjectionControlIndexWriter.StateKey(ControlIndexName);
        ReadModelEntry<SharedProjectionControlIndexState> prior = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        (await store.TryEraseAsync(s_scope.StoreName, key, prior.ETag!)).ShouldBeTrue();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(s_scope)).PendingDeliveryCount.ShouldBe(0);
        ReadModelEntry<SharedProjectionControlIndexState> restored = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        restored.Value!.TenantIds.ShouldBe(["tenant-a"]);
        restored.Value.ScopeReceipts.Count.ShouldBe(1);
        (await restarted.ReadAsync<SharedProjectionControlIndexIntent>(s_scope, "index"))
            .Value!.IndexName.ShouldBe(ControlIndexName);
    }

    [Fact]
    public async Task CapacityRefusesAcknowledgement_UntilAuditedOffboardingPrunesAMember()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var writer = new SharedProjectionControlIndexWriter(store);
        SharedProjectionLease firstLease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, firstLease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold());
        for (int number = 1; number < SharedProjectionControlIndexWriter.MaxTenants; number++)
        {
            SharedProjectionScope member = s_scope with { TenantId = $"tenant-{number:000}" };
            await writer.ApplyAsync(member, new SharedProjectionControlIndexIntent(ControlIndexName), CancellationToken.None);
        }

        SharedProjectionScope overflow = s_scope with { TenantId = "tenant-overflow" };
        SharedProjectionLease overflowLease = await coordinator.RegisterWriterAsync(overflow, "ordinary-dispatch");
        _ = await Should.ThrowAsync<SharedProjectionControlIndexCapacityException>(() =>
            coordinator.JournalAsync(overflow, overflowLease, Delivery(1)));
        ReadModelEntry<SharedProjectionEpochState> refused = await store
            .GetAsync<SharedProjectionEpochState>(overflow.StoreName, overflow.StateKey);
        refused.Value!.Journal.ShouldBeEmpty();
        ReadModelEntry<SharedProjectionControlIndexState> full = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, SharedProjectionControlIndexWriter.StateKey(ControlIndexName));
        full.Value!.TenantIds.Length.ShouldBe(SharedProjectionControlIndexWriter.MaxTenants);

        await coordinator.OffboardTenantAsync(s_scope, "audit-offboard-001");
        ReadModelEntry<SharedProjectionControlIndexTombstone> tombstone = await store
            .GetAsync<SharedProjectionControlIndexTombstone>(
                s_scope.StoreName,
                SharedProjectionControlIndexWriter.TombstoneKey(ControlIndexName, s_scope.TenantId));
        tombstone.Value!.AuditId.ShouldBe("audit-offboard-001");
        (await coordinator.GetStatusAsync(s_scope)).IsOffboarded.ShouldBeTrue();
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldNotContain(s_scope.TenantId);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.JournalAsync(s_scope, firstLease, Delivery(2)));

        (await coordinator.JournalAsync(overflow, overflowLease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName)).Count
            .ShouldBe(SharedProjectionControlIndexWriter.MaxTenants);
    }

    [Fact]
    public async Task InterruptedOffboarding_RestartReconcilesAuditedTombstoneAndPrune()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, lease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(s_scope, Fold());

        int writes = 0;
        store.ConcurrentWriteBeforeTrySave = () =>
        {
            if (Interlocked.Increment(ref writes) == 2)
            {
                throw new InvalidOperationException("crash before tombstone CAS");
            }
        };
        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            coordinator.OffboardTenantAsync(s_scope, "audit-offboard-002"));
        store.ConcurrentWriteBeforeTrySave = null;
        ReadModelEntry<SharedProjectionEpochState> fenced = await store
            .GetAsync<SharedProjectionEpochState>(s_scope.StoreName, s_scope.StateKey);
        fenced.Value!.OffboardingAuditId.ShouldBe("audit-offboard-002");

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.GetStatusAsync(s_scope)).IsOffboarded.ShouldBeTrue();
        ReadModelEntry<SharedProjectionControlIndexTombstone> tombstone = await store
            .GetAsync<SharedProjectionControlIndexTombstone>(
                s_scope.StoreName,
                SharedProjectionControlIndexWriter.TombstoneKey(ControlIndexName, s_scope.TenantId));
        tombstone.Value!.AuditId.ShouldBe("audit-offboard-002");
        (await restarted.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName)).ShouldBeEmpty();
        await restarted.OffboardTenantAsync(s_scope, "audit-offboard-002");
        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            restarted.OffboardTenantAsync(s_scope, "conflicting-audit"));
    }

    [Fact]
    public async Task NoncanonicalTenant_CannotCreateControlMembershipOrAcknowledgeDelivery()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope invalid = s_scope with { TenantId = "Tenant-A" };
        SharedProjectionLease lease = await coordinator.RegisterWriterAsync(invalid, "ordinary-dispatch");

        _ = await Should.ThrowAsync<ArgumentException>(() => coordinator.JournalAsync(invalid, lease, Delivery(1)));
        (await coordinator.GetControlIndexTenantsAsync(invalid.StoreName, ControlIndexName)).ShouldBeEmpty();
        ReadModelEntry<SharedProjectionEpochState> epoch = await store
            .GetAsync<SharedProjectionEpochState>(invalid.StoreName, invalid.StateKey);
        epoch.Value!.Journal.ShouldBeEmpty();
    }

    [Fact]
    public async Task ControlIndexNameCannotBeSharedByUnrelatedProjectionFamilies()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionLease firstLease = await coordinator.RegisterWriterAsync(s_scope, "ordinary-dispatch");
        (await coordinator.JournalAsync(s_scope, firstLease, Delivery(1)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        SharedProjectionScope otherFamily = s_scope with { TenantId = "tenant-b", Family = "other-index" };
        SharedProjectionLease secondLease = await coordinator.RegisterWriterAsync(otherFamily, "ordinary-dispatch");

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            coordinator.JournalAsync(otherFamily, secondLease, Delivery(1)));
        ReadModelEntry<SharedProjectionEpochState> refused = await store
            .GetAsync<SharedProjectionEpochState>(otherFamily.StoreName, otherFamily.StateKey);
        refused.Value!.Journal.ShouldBeEmpty();
        (await coordinator.GetControlIndexTenantsAsync(s_scope.StoreName, ControlIndexName))
            .ShouldBe(["tenant-a"]);
    }

    [Fact]
    public async Task AuthoritativeInventoryRestoresWholeControlIndexAfterJournalDrain()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        SharedProjectionScope second = s_scope with { TenantId = "tenant-b" };
        foreach (SharedProjectionScope scope in new[] { s_scope, second })
        {
            SharedProjectionLease lease = await coordinator.RegisterWriterAsync(scope, "ordinary-dispatch");
            (await coordinator.JournalAsync(scope, lease, Delivery(1)))
                .ShouldBe(SharedProjectionJournalResult.Journaled);
            (await coordinator.CatchUpAsync(scope, Fold())).ShouldBe(1);
        }

        string key = SharedProjectionControlIndexWriter.StateKey(ControlIndexName);
        ReadModelEntry<SharedProjectionControlIndexState> before = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        (await store.TryEraseAsync(s_scope.StoreName, key, before.ETag!)).ShouldBeTrue();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        (await restarted.ReconcileControlIndexInventoryAsync([s_scope, second])).ShouldBe(2);
        ReadModelEntry<SharedProjectionControlIndexState> restored = await store
            .GetAsync<SharedProjectionControlIndexState>(s_scope.StoreName, key);
        restored.Value!.TenantIds.ShouldBe(["tenant-a", "tenant-b"]);
        restored.Value.ScopeReceipts.Count.ShouldBe(2);
        _ = await Should.ThrowAsync<ArgumentException>(() =>
            restarted.ReconcileControlIndexInventoryAsync([s_scope, s_scope]));
    }

    private static SharedProjectionDelivery Delivery(long position)
        => new(
            "widget-a",
            position,
            JsonSerializer.SerializeToUtf8Bytes(1),
            JsonSerializer.SerializeToUtf8Bytes(position),
            new SharedProjectionControlIndexIntent(ControlIndexName));

    private static Func<SharedProjectionDelivery, long, CancellationToken, Task<IReadOnlyList<ReadModelBatchOperation>>> Fold()
        => (delivery, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>(
        [
            ReadModelBatchOperation.Write(
                "index",
                new SharedProjectionControlIndexIntent(ControlIndexName),
                ReadModelBatchConcurrency.LastWrite),
        ]);
}
