// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 8; recovery/barrier matrix.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Resolves indexed rewrap outcomes behind an acknowledged barrier without accessing plaintext keys.</summary>
public sealed class ReviewBarrierRewrapTests
{
    private const string Rewrap = "01J00000000000000000001001";
    private const string Barrier = "01J00000000000000000001002";

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ConfirmedOutcomeBehindClearedBarrierResolvesWorkAndAllowsIndexedInvalidationOrDeletion(bool active, bool missing, bool delete)
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        if (active)
        {
            await f.Reservations.AcquireCompletionAsync(write.Completion!);
            await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        }
        else { await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted); }
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(missing ? new PayloadBackendResult(PayloadBackendFailure.Denied)
            : new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        var rotation = Rotation(f);
        if (missing) { await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Rewrap, "fixture:version-02").AsTask()); }
        else { (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Rewrap, "fixture:version-02")).ShouldBeFalse(); }
        string address = PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1);
        WrappedDekV1 pending = (await f.Store.ReadAsync<WrappedDekV1>(address))!.Value;
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = Lifecycle(f, cache);
        var approval = LifecycleDrainTests.Approval(f.Identity);
        await lifecycle.BeginAsync(f.Identity, approval, Barrier, 2, delete);
        await lifecycle.AcknowledgeAsync(f.Identity, Barrier);
        Should.Throw<PayloadProtectionStateException>(() => f.Leases.LocalOperations.Begin(f.Identity));
        int unwraps = f.Backend.UnwrapCalls;
        f.Backend.Unwrap = (_, _, _, _) => throw new InvalidOperationException("No plaintext DEK may be obtained behind the barrier.");
        byte[] wrapped = Enumerable.Repeat((byte)0xd7, 32).ToArray();
        f.Backend.Reconcile = (operation, _) => { operation.ShouldBe(Rewrap); return ValueTask.FromResult(missing
            ? new PayloadBackendResult(PayloadBackendFailure.Missing) : new PayloadBackendResult(PayloadBackendFailure.None, wrapped, "fixture:version-02")); };
        var claims = new PayloadReconciliationClaims(f.Store, f.Clock);
        var claim = (await claims.TryClaimAsync(new(Rewrap, f.Scope)))!.Value;
        (await rotation.ReconcileAsync(f.Identity, claim)).ShouldBe(!missing);
        WrappedDekV1 retained = (await f.Store.ReadAsync<WrappedDekV1>(address))!.Value;
        retained.State.ShouldBe(missing ? (active ? "Active" : "Reserved") : "RewrapPending");
        retained.WrappedDek.ShouldBe(pending.WrappedDek); retained.ProviderKeyVersionId.ShouldBe(pending.ProviderKeyVersionId);
        if (!missing) { retained.ShouldBe(pending); wrapped.ShouldAllBe(b => b == 0); }
        f.Backend.UnwrapCalls.ShouldBe(unwraps); f.Backend.ReconcileCalls.ShouldBe(1);
        var indexes = new PayloadKeyIndexes(f.Store, f.Clock);
        PayloadKeyIndexes.VerifyMembership(retained, await indexes.ReadScopeAsync(f.Scope), await rotation.VerifyInventoryAsync("fixture:version-01"));
        (await rotation.VerifyInventoryAsync("fixture:version-02")).Head.ShouldBeNull();
        var result = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(Rewrap)))!.Value;
        result.Phase.ShouldBe(missing ? "Failed" : "Completed"); result.Result.ShouldBe(missing ? "Failed" : "Succeeded");
        result.ReasonCode.ShouldBe(missing ? "not-persisted" : "none");
        f.Store.Audits.Single(a => a.OperationId == Rewrap && a.Result == (missing ? "Failed" : "Succeeded") && a.WorkflowId is not null).WorkflowId.ShouldBe(approval.Identity.WorkflowId);
        var drainClaim = (await claims.TryClaimAsync(new(Barrier, f.Scope)))!.Value;
        (await lifecycle.ResumeAsync(f.Identity, drainClaim)).ShouldBeTrue();
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe(delete ? "Deleted" : "Invalidated");
        var key = await f.Store.ReadAsync<WrappedDekV1>(address);
        if (delete) { key.ShouldBeNull(); (await indexes.ReadScopeAsync(f.Scope)).Head!.Value.EntryCount.ShouldBe(0u); (await rotation.VerifyInventoryAsync("fixture:version-01")).Head!.Value.EntryCount.ShouldBe(0ul); }
        else { key!.Value.State.ShouldBe("Invalidated"); }
    }

    [Theory]
    [InlineData(PayloadBackendFailure.Unavailable)]
    [InlineData(PayloadBackendFailure.Ambiguous)]
    [InlineData(PayloadBackendFailure.Denied)]
    [InlineData(PayloadBackendFailure.None)]
    public async Task UncertainDeniedOrWrongTargetOutcomeRetainsAllKeyStateAndBlocksDeletion(int failureValue)
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        f.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Ambiguous));
        var rotation = Rotation(f);
        (await rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Rewrap, "fixture:version-02")).ShouldBeFalse();
        await using var cache = new PayloadKeyCache(timeProvider: f.Clock);
        var lifecycle = Lifecycle(f, cache);
        await lifecycle.BeginAsync(f.Identity, LifecycleDrainTests.Approval(f.Identity), Barrier, 2, true);
        await lifecycle.AcknowledgeAsync(f.Identity, Barrier);
        var claims = new PayloadReconciliationClaims(f.Store, f.Clock);
        var claim = (await claims.TryClaimAsync(new(Rewrap, f.Scope)))!.Value;
        var before = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1)))!;
        OperationV1 operation = (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(Rewrap)))!.Value;
        int unwraps = f.Backend.UnwrapCalls;
        var failure = (PayloadBackendFailure)failureValue;
        byte[] response = new byte[32];
        f.Backend.Reconcile = (_, _) => ValueTask.FromResult(failure == PayloadBackendFailure.None
            ? new PayloadBackendResult(failure, response, "fixture:wrong-target") : new PayloadBackendResult(failure));
        if (failure is PayloadBackendFailure.Denied or PayloadBackendFailure.None) { await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.ReconcileAsync(f.Identity, claim).AsTask()); }
        else { (await rotation.ReconcileAsync(f.Identity, claim)).ShouldBeFalse(); }
        var after = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion.KeyReference, 1)))!;
        after.Value.ShouldBe(before.Value); after.ETag.ShouldBe(before.ETag);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(Rewrap)))!.Value.ShouldBe(operation);
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Rewrap)))!.Value.ShouldBe(claim);
        f.Backend.UnwrapCalls.ShouldBe(unwraps);
        (await lifecycle.ResumeAsync(f.Identity, (await claims.TryClaimAsync(new(Barrier, f.Scope)))!.Value)).ShouldBeFalse();
        (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value.State.ShouldBe("Invalidating");
        response.ShouldAllBe(b => b == 0);
    }

    private static PayloadKeyRotationCoordinator Rotation(ReservationTestFixture f) => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
    private static KeyLifecycleCoordinator Lifecycle(ReservationTestFixture f, PayloadKeyCache cache) => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, cache,
        f.Backend.BackendId, _ => true, "fixture-actor", f.Clock, (duration, _) => { f.Clock.Advance(duration); return ValueTask.CompletedTask; });
}
