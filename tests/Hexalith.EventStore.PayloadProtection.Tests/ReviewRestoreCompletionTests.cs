// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 13.5; amendment section 8; pass-3 restore regressions.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Quarantine cannot regress, and confirmed durable decisions remain successful despite later cleanup failures.</summary>
public sealed class ReviewRestoreCompletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuarantineSurvivesAFinalAuthorityChangeOrStateException(bool stateException)
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        var key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        var fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        var lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        int lifecycleReads = 0;
        f.Store.Reading = address =>
        {
            if (address != PayloadProtectionStateKeys.Lifecycle(f.Scope) || ++lifecycleReads != 2) { return; }
            f.Store.Reading = null;
            if (stateException) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderUnavailable); }
            var current = f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)).GetAwaiter().GetResult()!;
            f.Store.CommitAsync([PayloadProtectionStateMutation.Compare(current)]).GetAwaiter().GetResult().ShouldBe(PayloadStateCommitResult.Committed);
        };
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            async (decision, conditions, token) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.Quarantined); conditions.ShouldBeEmpty();
                await f.Store.CommitAsync([PayloadProtectionStateMutation.Put(Audit(f, "Failed"))], token);
                return decision;
            }, f.Clock);
        (await admission.AdmitAsync(Request(f), fence with { HighestWrittenEpoch = 0 }, lifecycle, [key])).State.ShouldBe(RestoredBackupAdmissionState.Quarantined);
        lifecycleReads.ShouldBe(2); f.Backend.UnwrapCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RecordedDecisionReturnsDespiteSubsequentCancellationDeadlineAndLeaseReleaseFailure()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        var key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, write.Completion!.KeyReference, 1)))!.Value;
        var fence = (await f.Store.ReadAsync<FenceV1>(PayloadProtectionStateKeys.Fence(f.Root)))!.Value;
        var lifecycle = (await f.Store.ReadAsync<LifecycleV1>(PayloadProtectionStateKeys.Lifecycle(f.Scope)))!.Value;
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        using var cancelled = new CancellationTokenSource();
        bool releaseFailed = false;
        var admission = new PayloadRestoreAdmission(f.Store, new([new("tenant-a", "parties")]), f.Backend, new(f.Clock), f.Leases, _ => true,
            async (decision, conditions, token) =>
            {
                decision.State.ShouldBe(RestoredBackupAdmissionState.Accepted);
                (await f.Store.CommitAsync([.. conditions, PayloadProtectionStateMutation.Put(Audit(f, "Succeeded"))], token)).ShouldBe(PayloadStateCommitResult.Committed);
                f.Store.Reading = address => { if (address.StartsWith("eventstore:pp:v2:lease:", StringComparison.Ordinal)) { releaseFailed = true; throw new InvalidOperationException(); } };
                f.Clock.Advance(TimeSpan.FromSeconds(10)); cancelled.Cancel();
                return decision;
            }, f.Clock);
        (await admission.AdmitAsync(Request(f), fence, lifecycle, [key], cancelled.Token)).State.ShouldBe(RestoredBackupAdmissionState.Accepted);
        releaseFailed.ShouldBeTrue(); f.Store.Audits.ShouldContain(a => a.AuditId == Request(f).AdmissionId && a.Result == "Succeeded");
    }

    private static RestoredBackupAdmissionRequest Request(ReservationTestFixture f) => new("01J00000000000000000009301", "tenant-a", "parties", "party-01", null, null,
        "01J00000000000000000009302", f.Clock.GetUtcNow(), f.Clock.GetUtcNow(), 1, KeyReferencePolicy.NoKeyReference, null, null, null, "fixture-actor");
    private static PayloadProtectionAuditRecord Audit(ReservationTestFixture f, string result) => new("01J00000000000000000009301", "01J00000000000000000009301", null, null,
        "tenant-a", "parties", null, "Reconcile", null, null, 1, 7, f.Backend.BackendId, "v2", null, null, 1, 0, result, result == "Succeeded" ? "none" : "consistency-mismatch",
        "fixture-actor", PayloadProtectionStateCodec.Timestamp(f.Clock.GetUtcNow()));
}
