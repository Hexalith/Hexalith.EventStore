// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 8; V2.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Restore authority races at provider verification and at conditional decision persistence.</summary>
public sealed class ReviewRestoreTests
{
    private const string Admission = "01J00000000000000000000801";

    [Theory]
    [InlineData("lifecycle:")]
    [InlineData("fence:")]
    [InlineData("key:")]
    [InlineData("index-head:")]
    [InlineData("index:")]
    [InlineData("kek-index-head:")]
    [InlineData("kek-index:")]
    public async Task AcceptedDecisionAtomicallyComparesEverySupportingObservationAtTheDecisionBoundary(string changed)
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        WrappedDekV1 key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        FenceV1 fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        LifecycleV1 lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        bool accepted = false;
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            async (decision, conditions, token) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.Accepted);
                conditions.Count.ShouldBe(7);
                PayloadProtectionStateMutation stale = conditions.Single(c => c.Key.StartsWith("eventstore:pp:v2:" + changed, StringComparison.Ordinal));
                // This boundary change is persisted before the existing workflow's attempted atomic decision.
                (await f.Store.CommitAsync([stale], token)).ShouldBe(PayloadStateCommitResult.Committed);
                (await f.Store.CommitAsync([.. conditions, PayloadProtectionStateMutation.Put(Audit(f, "Succeeded"))], token)).ShouldBe(PayloadStateCommitResult.Conflict);
                if (f.Store.Audits.Any(a => a.AuditId == Admission)) { accepted = true; }
                throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ConsistencyMismatch);
            }, f.Clock);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => admission.AdmitAsync(Request(f), fence, lifecycle, [key]).AsTask());
        accepted.ShouldBeFalse(); f.Store.Audits.ShouldNotContain(a => a.AuditId == Admission);
        (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, Admission))).ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V2_AuthorityMutationDuringUnwrapNeverProducesAnAcceptedDecision(bool changeFence)
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        WrappedDekV1 key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        FenceV1 fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        LifecycleV1 lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        byte[] response = Enumerable.Repeat((byte)0xcd, 32).ToArray();
        f.Backend.Unwrap = async (_, provider, _, token) =>
        {
            (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, Admission), token)).ShouldNotBeNull();
            if (changeFence)
            {
                var live = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root), token))!;
                await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(live.Value with { ApprovalEpoch = live.Value.ApprovalEpoch + 1 }, live.ETag)], token);
            }
            else
            {
                var live = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope), token))!;
                await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(live.Value with { State = "Invalidating", Epoch = 2, ReasonCode = "domain-invalidating" }, live.ETag)], token);
            }
            return new(PayloadBackendFailure.None, response, provider);
        };
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            async (decision, conditions, token) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.OperatorDecisionRequired); conditions.ShouldBeEmpty();
                (await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(Audit(f, "Failed"))], token)).ShouldBe(PayloadStateCommitResult.Committed);
                return decision;
            }, f.Clock);
        (await admission.AdmitAsync(Request(f), fence, lifecycle, [key])).State.ShouldBe(RestoredBackupAdmissionState.OperatorDecisionRequired);
        response.ShouldAllBe(b => b == 0);
        f.Store.Audits.ShouldNotContain(a => a.AuditId == Admission && a.Result == "Succeeded");
        (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, key.KeyRef, 1)))!.Value.ShouldBe(key);
        (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, Admission))).ShouldBeNull();
    }

    [Fact]
    public async Task CancellationIgnoringRestoreBackendRemainsInLocalClearingProofUntilItsLateOutputIsZeroed()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        WrappedDekV1 key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        FenceV1 fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        LifecycleV1 lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.Unwrap = (_, _, _, _) => { entered.SetResult(); return new(late.Task); };
        int decisions = 0;
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            (_, _, _) => { decisions++; throw new InvalidOperationException(); }, f.Clock);
        using var cancellation = new CancellationTokenSource();
        Task<RestoredBackupAdmissionResult> pending = admission.AdmitAsync(Request(f), fence, lifecycle, [key], cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, Admission))).ShouldNotBeNull();
        cancellation.Cancel(); await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
        Task clearing = f.Leases.LocalOperations.DrainScopeAsync(f.Scope).AsTask();
        clearing.IsCompleted.ShouldBeFalse(); decisions.ShouldBe(0);
        byte[] bytes = Enumerable.Repeat((byte)0x5e, 32).ToArray();
        late.SetResult(new(PayloadBackendFailure.None, bytes, key.ProviderKeyVersionId));
        await clearing.WaitAsync(TimeSpan.FromSeconds(2)); bytes.ShouldAllBe(b => b == 0);
        (await f.Store.ReadAsync<ReadLeaseV1>(PayloadProtectionStateKeys.Lease(f.Scope, Admission))).ShouldBeNull();
    }

    private static RestoredBackupAdmissionRequest Request(ReservationTestFixture f) => new(Admission, "tenant-a", "parties", "party-01", null, null,
        "01J00000000000000000000802", f.Clock.GetUtcNow(), f.Clock.GetUtcNow(), 1, KeyReferencePolicy.NoKeyReference, null, null, null, "fixture-actor");
    private static PayloadProtectionAuditRecord Audit(ReservationTestFixture f, string result) => new(Admission, Admission, Admission, null, "tenant-a", "parties", null,
        "Reconcile", null, null, 1, 7, f.Backend.BackendId, "v2", null, null, 1, 0, result, result == "Succeeded" ? "none" : "consistency-mismatch", "fixture-actor", PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow()));
}
