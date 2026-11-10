// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 8.3, 10.4 and 15; owner decision D1 (2026-11-10).
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Per-key rewrap verifies only its own memberships yet stays bound to the coordinator's backend without whole-inventory reads.</summary>
public sealed class RewrapMembershipTests
{
    private const string Operation = "01J00000000000000000000721";
    private const string ForeignBackend = "foreign-backend";

    [Fact]
    public async Task RecoveryRefusesARewrapPendingKeyBoundToAnotherBackendEvenWithItsForeignMembership()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:wrong-target"));
        PayloadKeyRotationCoordinator rotation = Rotation(f);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02").AsTask());
        string keyAddress = PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1);
        WrappedDekV1 foreign = await RebindToForeignBackendAsync(f, keyAddress, "fixture:version-01");
        foreign.State.ShouldBe("RewrapPending");
        f.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(Operation, f.Scope)))!.Value;
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.ReconcileAsync(f.Identity, claim).AsTask())).Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
        (await f.Store.ReadAsync<WrappedDekV1>(keyAddress))!.Value.ShouldBe(foreign);
        (await f.Store.ReadAsync<KekInventoryHeadV1>(PayloadProtectionStateKeys.KekHead(PayloadProtectionStateKeys.Digest(f.Backend.BackendId), PayloadProtectionStateKeys.Digest("fixture:version-02")))).ShouldBeNull();
    }

    [Fact]
    public async Task ACompletedRewrapReplayRefusesAKeyBoundToAnotherBackendEvenWithItsForeignMembership()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        PayloadKeyRotationCoordinator rotation = Rotation(f);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02")).ShouldBeTrue();
        // The replay checks only the completed key's own bucket-page memberships, so it must also bind the record to this backend.
        (await rotation.RewrapAsync(f.Identity, write.Completion.KeyReference, 1, Operation, "fixture:version-02")).ShouldBeTrue();
        await RebindToForeignBackendAsync(f, PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1), "fixture:version-02");
        (await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion.KeyReference, 1, Operation, "fixture:version-02").AsTask()))
            .Reason.ShouldBe(UnreadableProtectedDataReason.ConsistencyMismatch);
    }

    [Fact]
    public async Task RewrapIntoATargetInventoryAtAGrowthStepSplitsItsBucketAndPlacesTheEntry()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        string backend = PayloadProtectionStateKeys.Digest(f.Backend.BackendId);
        string target = PayloadProtectionStateKeys.Digest("fixture:version-02");
        // 128 = 64 x 2 memberships: the rewrap's insertion splits bucket 0 into the new page 2, while its own bucket stays page 1.
        (await f.Store.CommitAsync(IndexSeed.Inventory(backend, target, 2, IndexSeed.Entries(128)))).ShouldBe(PayloadStateCommitResult.Committed);
        string keyRef = Enumerable.Range(1, 64).Select(i => "01J" + i.ToString("D23", System.Globalization.CultureInfo.InvariantCulture))
            .First(k => IndexPlacementTests.Bucket(new KekInventoryEntryV1(f.Scope, k, 1), 2) == 1);
        var roots = new PayloadProtectionScopeRegistry([new("tenant-a", "parties")]);
        var entropy = new SequenceEntropy([keyRef, .. Enumerable.Range(9600, 64).Select(i => "01J" + i.ToString("D23", System.Globalization.CultureInfo.InvariantCulture))]);
        var reservations = new KeyReservationCoordinator(f.Store, roots, f.Leases, new(roots, [new("parties", f.Erasure)]), f.Backend, new(f.Clock),
            _ => true, "fixture-actor", timeProvider: f.Clock, entropy: entropy);
        var write = await reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        write.Completion!.KeyReference.ShouldBe(keyRef);
        await reservations.AcquireCompletionAsync(write.Completion);
        await reservations.CompleteAsync(write.Completion, PayloadProtectionPersistenceOutcome.Persisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        (await Rotation(f).RewrapAsync(f.Identity, keyRef, 1, Operation, "fixture:version-02")).ShouldBeTrue();
        KekKeyIndexSnapshot grown = await new PayloadKeyIndexes(f.Store, f.Clock).ReadKekAsync(backend, target);
        grown.Head!.Value.PageCount.ShouldBe(3u);
        grown.Head.Value.EntryCount.ShouldBe(129ul);
        var entry = new KekInventoryEntryV1(f.Scope, keyRef, 1);
        IndexPlacementTests.Bucket(entry, 3).ShouldBe(1u);
        grown.Pages[1].Value.Entries.ShouldContain(entry);
    }

    private static async Task<WrappedDekV1> RebindToForeignBackendAsync(ReservationTestFixture f, string keyAddress, string provider)
    {
        PayloadProtectionStateValue<WrappedDekV1> current = (await f.Store.ReadAsync<WrappedDekV1>(keyAddress))!;
        WrappedDekV1 foreign = PayloadProtectionStateCodec.Seal(current.Value with { BackendId = ForeignBackend });
        // The foreign inventory holds the key's exact membership, so only the explicit backend binding can refuse it.
        (await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(foreign, current.ETag),
            .. IndexSeed.Inventory(PayloadProtectionStateKeys.Digest(ForeignBackend), PayloadProtectionStateKeys.Digest(provider), 1,
                [new KekInventoryEntryV1(f.Scope, foreign.KeyRef, foreign.DekVersion)])])).ShouldBe(PayloadStateCommitResult.Committed);
        return foreign;
    }

    private static PayloadKeyRotationCoordinator Rotation(ReservationTestFixture f)
        => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
}
