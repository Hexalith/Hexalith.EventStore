// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4 and 15; owner decision D1 (2026-11-10).
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Single-key work reads only the inventory head twice and the key's bucket page, whatever the environment-wide inventory size.</summary>
public sealed class IndexCapacityTests
{
    private const uint Pages = 313;
    private const string Version = "fixture:version-01";
    private const string RewrapOperation = "01J00000000000000000000743";

    [Fact]
    public async Task ReservationReadsOnlyItsBucketPageAndPlansWithinTransactionCapacity()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        List<string> reads = Record(f, backend, provider);
        IReadOnlyList<PayloadProtectionStateMutation>? reservation = null;
        f.Store.Committing = mutations =>
        {
            // Only the reservation transaction itself writes the inventory.
            if (!mutations.Any(m => m.Key.StartsWith("eventstore:pp:v2:kek-index", StringComparison.Ordinal))) { return; }
            reservation.ShouldBeNull();
            reservation = mutations;
            PayloadProtectionOperationalLimits.RequireTransactionCapacity(mutations);
        };
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        f.Store.Reading = null; f.Store.Committing = null;
        KekInventoryEntryV1 entry = new(f.Scope, write.Completion!.KeyReference, 1);
        AssertPointReads(reads, backend, provider, entry, 1);
        reservation.ShouldNotBeNull();
        reservation.Where(m => m.Key.StartsWith("eventstore:pp:v2:kek-index", StringComparison.Ordinal)).Select(m => m.Key)
            .ShouldBe([PayloadProtectionStateKeys.KekHead(backend, provider), PayloadProtectionStateKeys.KekPage(backend, provider, IndexPlacementTests.Bucket(entry, Pages))]);
        // A complete read of 313 maximum-size pages exceeds one read phase; the head alone carries the count.
        (await f.Store.ReadAsync<KekInventoryHeadV1>(PayloadProtectionStateKeys.KekHead(backend, provider)))!.Value.EntryCount.ShouldBe(20_001ul);
    }

    [Fact]
    public async Task ResolutionReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], version));
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var resolver = new PayloadKeyResolver(f.Store, roots, new(roots, [new("parties", f.Erasure)]), f.Leases, f.Backend, new(f.Clock), cache, f.Clock);
        List<string> reads = Record(f, backend, provider);
        await using (PayloadKeyResolution resolution = await resolver.ResolveAsync(f.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000000701"))
        {
            resolution.Reason.ShouldBeNull();
            CryptographicOperations.ZeroMemory(resolution.TakeBytes());
        }
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion.KeyReference, 1), 1);
    }

    [Fact]
    public async Task ActivationReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        List<string> reads = Record(f, backend, provider);
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1)))!.Value.State.ShouldBe("Active");
    }

    [Fact]
    public async Task RewrapReadsOnlyItsBucketPageOfTheRetiringInventory()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        byte[]? dek = null;
        f.Backend.Wrap = (_, bytes, _) => { dek = bytes.ToArray(); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], Version)); };
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, dek!.ToArray(), version));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, Enumerable.Repeat((byte)2, 32).ToArray(), "fixture:version-02"));
        var rotation = new PayloadKeyRotationCoordinator(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
        List<string> reads = Record(f, backend, provider);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, "01J00000000000000000000702", "fixture:version-02")).ShouldBeTrue();
        // One membership check when the intent is recorded and one in the commit; no whole-inventory verification.
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion.KeyReference, 1), 2);
        (await f.Store.ReadAsync<KekInventoryHeadV1>(PayloadProtectionStateKeys.KekHead(backend, provider)))!.Value.EntryCount.ShouldBe(20_000ul);
        (await new PayloadKeyIndexes(f.Store, f.Clock).ReadKekAsync(backend, PayloadProtectionStateKeys.Digest("fixture:version-02"))).Head!.Value.EntryCount.ShouldBe(1ul);
        CryptographicOperations.ZeroMemory(dek!);
    }

    [Fact]
    public async Task ReconciliationReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Unknown);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        using var worker = new PayloadProtectionReconciler(f.Store, roots, Lifecycle(f, cache), f.Backend.BackendId, _ => true, "fixture-actor", f.Clock,
            persistence: new LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome.Persisted));
        List<string> reads = Record(f, backend, provider);
        (await worker.RunPassAsync()).ShouldBe(1);
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1)))!.Value.State.ShouldBe("Active");
    }

    [Fact]
    public async Task QuarantineReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        using var worker = new PayloadProtectionReconciler(f.Store, roots, Lifecycle(f, cache), f.Backend.BackendId, _ => true, "fixture-actor", f.Clock);
        // At the 24-hour orphan boundary the retained reservation is quarantined against its own bucket page only.
        f.Clock.Advance(TimeSpan.FromHours(24));
        List<string> reads = Record(f, backend, provider);
        (await worker.RunPassAsync()).ShouldBe(1);
        f.Store.Reading = null;
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(write.Completion.OperationId)))!.Value.Status.ShouldBe("Quarantined");
    }

    [Fact]
    public async Task InvalidationReadsOnlyItsBucketPageForTheChangeAndTheFinalCheck()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        KeyLifecycleCoordinator lifecycle = Lifecycle(f, cache);
        const string barrier = "01J00000000000000000000703";
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), barrier, 2);
        await lifecycle.AcknowledgeAsync(f.Identity, barrier);
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(barrier, f.Scope)))!.Value;
        List<string> reads = Record(f, backend, provider);
        (await lifecycle.ResumeAsync(f.Identity, claim)).ShouldBeTrue();
        // One membership check before the key changes and one batched head-confirmed read before the irreversible result.
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 2);
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe("Invalidated");
    }

    [Fact]
    public async Task RestoreAdmissionComparesTheCompleteScopeIndexButOnlyTheInventoryHeadAndBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        WrappedDekV1 key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        FenceV1 fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        LifecycleV1 lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], version));
        IReadOnlyList<PayloadProtectionStateMutation>? recorded = null;
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            (decision, conditions, _) => { recorded = conditions; return ValueTask.FromResult(decision); }, f.Clock);
        List<string> reads = Record(f, backend, provider);
        var request = new RestoredBackupAdmissionRequest("01J00000000000000000000705", "tenant-a", "parties", "party-01", null, null,
            "01J00000000000000000000706", f.Clock.GetUtcNow(), f.Clock.GetUtcNow(), 1, KeyReferencePolicy.NoKeyReference, null, null, null, "fixture-actor");
        (await admission.AdmitAsync(request, fence, lifecycle, [key])).State.ShouldBe(RestoredBackupAdmissionState.Accepted);
        var entry = new KekInventoryEntryV1(f.Scope, key.KeyRef, 1);
        AssertPointReads(reads, backend, provider, entry, 1);
        recorded.ShouldNotBeNull();
        recorded.Where(c => c.Key.StartsWith("eventstore:pp:v2:kek-index", StringComparison.Ordinal)).Select(c => c.Key)
            .ShouldBe([PayloadProtectionStateKeys.KekHead(backend, provider), PayloadProtectionStateKeys.KekPage(backend, provider, IndexPlacementTests.Bucket(entry, Pages))]);
        recorded.Where(c => c.Key.StartsWith("eventstore:pp:v2:index", StringComparison.Ordinal)).Select(c => c.Key)
            .ShouldBe([PayloadProtectionStateKeys.IndexHead(f.Scope), PayloadProtectionStateKeys.IndexPage(f.Scope, 0)]);
    }

    [Fact]
    public async Task PointReadOfAnInventoryTooLargeForAFullReadReadsTheHeadTwiceAndOneBucketPage()
    {
        var clock = new LifecycleTestClock(); var store = new MemoryPayloadProtectionStateStore(clock);
        string backend = new('a', 64); string provider = new('b', 64);
        var entry = new KekInventoryEntryV1(new('c', 64), "01J00000000000000000000704", 1);
        uint bucket = IndexPlacementTests.Bucket(entry, 256);
        await store.CommitAsync([PayloadProtectionStateMutation.Put(new KekInventoryHeadV1(backend, provider, 1, 256, 0, IndexSeed.Timestamp)),
            PayloadProtectionStateMutation.Put(new KekInventoryPageV1(backend, provider, 1, bucket, []))]);
        var indexes = new PayloadKeyIndexes(store, clock);
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => indexes.ReadKekAsync(backend, provider).AsTask())).IsAdmissionLimit.ShouldBeTrue();
        var reads = new List<string>(); store.Reading = reads.Add;
        KekInventoryView view = await indexes.ReadInventoryEntryAsync(backend, provider, entry);
        reads.ShouldBe([PayloadProtectionStateKeys.KekHead(backend, provider), PayloadProtectionStateKeys.KekPage(backend, provider, bucket), PayloadProtectionStateKeys.KekHead(backend, provider)]);
        view.Pages.Keys.ShouldBe([bucket]);
    }

    [Fact]
    public async Task APointReadAcrossHundredsOfBucketPagesPreflightsPerChunkAndReadsEachPageOnce()
    {
        var clock = new LifecycleTestClock(); var store = new MemoryPayloadProtectionStateStore(clock);
        string backend = new('a', 64); string provider = new('b', 64);
        KekInventoryEntryV1[] seeded = IndexSeed.Entries(20_000);
        await store.CommitAsync(IndexSeed.Inventory(backend, provider, Pages, seeded));
        KekInventoryEntryV1[] wanted = seeded[..600];
        uint[] distinct = [.. wanted.Select(e => IndexPlacementTests.Bucket(e, Pages)).Distinct().Order()];
        distinct.Length.ShouldBeGreaterThanOrEqualTo(250);
        // One preflight of every selected maximum-size page would exceed the phase's 16 MiB read budget.
        ((distinct.Length + 1L) * PayloadProtectionStateCodec.MaximumBytes).ShouldBeGreaterThan(PayloadProtectionOperationalLimits.ReadBytes);
        var reads = new List<string>(); store.Reading = reads.Add;
        KekInventoryView view = await new PayloadKeyIndexes(store, clock).ReadInventoryEntriesAsync(backend, provider, wanted);
        string head = PayloadProtectionStateKeys.KekHead(backend, provider);
        reads.ShouldBe([head, .. distinct.Select(page => PayloadProtectionStateKeys.KekPage(backend, provider, page)), head]);
        view.Pages.Keys.ShouldBe(distinct);
    }

    [Fact]
    public async Task ConfirmedMissingRewrapRestorationReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, _, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Denied));
        PayloadKeyRotationCoordinator rotation = Rotation(f);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, RewrapOperation, "fixture:version-02").AsTask());
        f.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Missing));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(RewrapOperation, f.Scope)))!.Value;
        List<string> reads = Record(f, backend, provider);
        (await rotation.ReconcileAsync(f.Identity, claim)).ShouldBeFalse();
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1)))!.Value.State.ShouldBe("Reserved");
    }

    [Fact]
    public async Task RewrapResolutionBehindABarrierReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], version));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        PayloadKeyRotationCoordinator rotation = Rotation(f);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, RewrapOperation, "fixture:version-02")).ShouldBeFalse();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        KeyLifecycleCoordinator lifecycle = Lifecycle(f, cache);
        const string barrier = "01J00000000000000000000744";
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), barrier, 2);
        await lifecycle.AcknowledgeAsync(f.Identity, barrier);
        f.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, Enumerable.Repeat((byte)0xd7, 32).ToArray(), "fixture:version-02"));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(RewrapOperation, f.Scope)))!.Value;
        List<string> reads = Record(f, backend, provider);
        (await rotation.ReconcileAsync(f.Identity, claim)).ShouldBeTrue();
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion.KeyReference, 1), 1);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(RewrapOperation)))!.Value.Phase.ShouldBe("Completed");
    }

    [Fact]
    public async Task CompletedRewrapReplayReadsOnlyItsBucketPageOfTheTargetInventory()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string target) = await SeedAsync(f, "fixture:version-02");
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        f.Backend.Unwrap = (_, version, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], version));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        PayloadKeyRotationCoordinator rotation = Rotation(f);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, RewrapOperation, "fixture:version-02")).ShouldBeTrue();
        List<string> reads = Record(f, backend, target);
        (await rotation.RewrapAsync(f.Identity, write.Completion.KeyReference, 1, RewrapOperation, "fixture:version-02")).ShouldBeTrue();
        AssertPointReads(reads, backend, target, new(f.Scope, write.Completion.KeyReference, 1), 1);
    }

    [Fact]
    public async Task ActiveCompletionReplayReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        f.Store.LoseNextAcknowledgement = true;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted).AsTask());
        List<string> reads = Record(f, backend, provider);
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
    }

    [Fact]
    public async Task PersistedCompletionUnderAnInvalidationBarrierReadsOnlyItsBucketPage()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.AcquireCompletionAsync(write.Completion!);
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        await Lifecycle(f, cache).BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), "01J00000000000000000000745", 2);
        List<string> reads = Record(f, backend, provider);
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        AssertPointReads(reads, backend, provider, new(f.Scope, write.Completion!.KeyReference, 1), 1);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(write.Completion.OperationId)))!.Value.Phase.ShouldBe("Persisted");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionReadsOnlyItsBucketPages(bool singleKey)
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        (string backend, string provider) = await SeedAsync(f);
        // For one key, other memberships spread the scope index over four pages.
        if (singleKey) { await f.Store.CommitAsync(IndexSeed.Scope(f.Scope, 4, [.. IndexSeed.Entries(200).Select(e => new ScopeIndexEntryV1(e.KeyRef, 1, provider, "Reserved"))])); }
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        string keyRef = write.Completion!.KeyReference;
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        KeyLifecycleCoordinator lifecycle = Lifecycle(f, cache);
        const string barrier = "01J00000000000000000000746";
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), barrier, 2, delete: !singleKey);
        await lifecycle.AcknowledgeAsync(f.Identity, barrier);
        string operation = barrier;
        if (singleKey)
        {
            operation = "01J00000000000000000000747";
            string now = PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow());
            var catalogs = new PayloadProtectionCatalogs(f.Store, f.Clock);
            (await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(new OperationV1(operation, "Delete", f.Scope, keyRef, 1, "Started", 1, now, now, "Pending", "none")),
                PayloadProtectionStateMutation.Put(new ReconciliationV1(operation, f.Scope, keyRef, 1, "Delete", now, 0, "Pending", "none", now)),
                .. catalogs.AddWork(await catalogs.ReadWorkAsync(f.Root), new(operation, f.Scope))])).ShouldBe(PayloadStateCommitResult.Committed);
        }
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(operation, f.Scope)))!.Value;
        List<string> reads = Record(f, backend, provider);
        Action<string> inventoryReads = f.Store.Reading!;
        string scopePagePrefix = PayloadProtectionStateKeys.IndexPage(f.Scope, 0)[..^10];
        var scopePages = new List<string>();
        f.Store.Reading = key => { inventoryReads(key); if (key.StartsWith(scopePagePrefix, StringComparison.Ordinal)) { scopePages.Add(key); } };
        (await lifecycle.ResumeAsync(f.Identity, claim)).ShouldBeTrue();
        // The removal reads its own bucket page; a deletion's final check holds no remaining membership to read.
        AssertPointReads(reads, backend, provider, new(f.Scope, keyRef, 1), 1);
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, keyRef, 1))).ShouldBeNull();
        (await f.Store.ReadAsync<KekInventoryHeadV1>(PayloadProtectionStateKeys.KekHead(backend, provider)))!.Value.EntryCount.ShouldBe(20_000ul);
        if (singleKey)
        {
            // Single-key work, including its final check, reads no scope page other than the key's bucket page.
            uint bucket = PayloadIndexPlacement.Bucket(PayloadProtectionStateCodec.ScopeEntryIdentity(keyRef, 1), 4);
            scopePages.Distinct().ShouldBe([PayloadProtectionStateKeys.IndexPage(f.Scope, bucket)]);
            scopePages.Count.ShouldBe(3);
        }
    }

    private static async Task<(string Backend, string Provider)> SeedAsync(ReservationTestFixture f, string version = Version)
    {
        string backend = PayloadProtectionStateKeys.Digest(f.Backend.BackendId);
        string provider = PayloadProtectionStateKeys.Digest(version);
        // 20,000 memberships of other scopes on 313 placed pages; one more entry does not trigger a growth step.
        (await f.Store.CommitAsync(IndexSeed.Inventory(backend, provider, Pages, IndexSeed.Entries(20_000)))).ShouldBe(PayloadStateCommitResult.Committed);
        PayloadIndexPlacement.Grows(Pages, 20_000).ShouldBeFalse();
        return (backend, provider);
    }

    private static List<string> Record(ReservationTestFixture f, string backend, string provider)
    {
        var reads = new List<string>();
        (string head, string prefix) = IndexSeed.InventoryKeys(backend, provider);
        f.Store.Reading = key => { if (key == head || key.StartsWith(prefix, StringComparison.Ordinal)) { reads.Add(key); } };
        return reads;
    }

    private static void AssertPointReads(IReadOnlyList<string> reads, string backend, string provider, KekInventoryEntryV1 entry, int count)
    {
        string head = PayloadProtectionStateKeys.KekHead(backend, provider);
        string page = PayloadProtectionStateKeys.KekPage(backend, provider, IndexPlacementTests.Bucket(entry, Pages));
        reads.ShouldBe([.. Enumerable.Range(0, count).SelectMany(_ => new[] { head, page, head })]);
    }

    private static PayloadKeyRotationCoordinator Rotation(ReservationTestFixture f)
        => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);

    private static KeyLifecycleCoordinator Lifecycle(ReservationTestFixture f, PayloadKeyCache cache) => new(f.Store,
        new([new("tenant-a", "parties")]), f.Leases, cache, f.Backend.BackendId, _ => true, "fixture-actor", f.Clock,
        (duration, _) => { f.Clock.Advance(duration); return ValueTask.CompletedTask; });
}
