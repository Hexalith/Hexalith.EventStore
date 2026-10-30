// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment section 8; V1.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Retained targets, confirmed absent mutation and independently unequal valid DEK rejection.</summary>
public sealed class ReviewRewrapTests
{
    private const string Operation = "01J00000000000000000000701";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedMissingRewrapRestoresTheOriginalStateAndMaterialAndTerminalizesOnlyItsWork(bool active)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        if (active)
        {
            await fixture.Reservations.AcquireCompletionAsync(write.Completion!);
            await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.Persisted);
        }
        else { await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted); }
        string keyAddress = PayloadProtectionStateKeys.Key(fixture.Scope, write.Completion!.KeyReference, 1);
        WrappedDekV1 original = (await fixture.Store.ReadAsync<WrappedDekV1>(keyAddress))!.Value;
        fixture.Backend.Unwrap = (_, _, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Denied));
        var rotation = Rotation(fixture);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(fixture.Identity, original.KeyRef, 1, Operation, "fixture:version-02").AsTask());
        fixture.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Missing));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        (await rotation.ReconcileAsync(fixture.Identity, claim)).ShouldBeFalse();
        WrappedDekV1 restored = (await fixture.Store.ReadAsync<WrappedDekV1>(keyAddress))!.Value;
        restored.State.ShouldBe(original.State); restored.ProviderKeyVersionId.ShouldBe(original.ProviderKeyVersionId);
        restored.WrappedDek.ShouldBe(original.WrappedDek); restored.WrappedDekSha256.ShouldBe(original.WrappedDekSha256);
        PayloadKeyIndexes.VerifyMembership(restored, await new PayloadKeyIndexes(fixture.Store, fixture.Clock).ReadScopeAsync(fixture.Scope), await rotation.VerifyInventoryAsync(original.ProviderKeyVersionId));
        (await rotation.VerifyInventoryAsync("fixture:version-02")).Head.ShouldBeNull();
        (await fixture.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(Operation)))!.Value.ReasonCode.ShouldBe("not-persisted");
        (await fixture.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Operation)))!.Value.Status.ShouldBe("Completed");
        (await new PayloadProtectionCatalogs(fixture.Store, fixture.Clock).ReadWorkAsync(fixture.Root)).Pages.SelectMany(p => p.Value.Entries).ShouldNotContain(e => e.OperationId == Operation);
        fixture.Backend.WrapCalls.ShouldBe(1);
    }

    [Fact]
    public async Task WrongTargetRejectedDirectlyCannotBeCommittedByRestartRecovery()
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        fixture.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        fixture.Backend.Wrap = (_, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:wrong-target"));
        var rotation = Rotation(fixture);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02").AsTask());
        fixture.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:wrong-target"));
        ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
        claim.TargetProviderVersionDigest.ShouldBe(PayloadProtectionStateKeys.Digest("fixture:version-02"));
        int unwraps = fixture.Backend.UnwrapCalls;
        await Should.ThrowAsync<PayloadProtectionStateException>(() => Rotation(fixture).ReconcileAsync(fixture.Identity, claim).AsTask());
        fixture.Backend.UnwrapCalls.ShouldBe(unwraps); fixture.Backend.WrapCalls.ShouldBe(2);
        await AssertRetainedAsync(fixture, write.Completion!, rotation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task V1_DifferentValidDEKIsRejectedInBothDirectAndRecoveredRewrap(bool recovered)
    {
        ReservationTestFixture fixture = await ReservationTestFixture.CreateAsync();
        var write = await fixture.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await fixture.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        var outputs = new List<byte[]>();
        fixture.Backend.Unwrap = (_, provider, _, _) =>
        {
            byte[] bytes = Enumerable.Repeat(provider == "fixture:version-01" ? (byte)1 : (byte)2, 32).ToArray();
            outputs.Add(bytes); return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, bytes, provider));
        };
        fixture.Backend.Wrap = (_, _, _) => ValueTask.FromResult(recovered ? new PayloadBackendResult(PayloadBackendFailure.Ambiguous)
            : new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        var rotation = Rotation(fixture);
        if (recovered)
        {
            (await rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02")).ShouldBeFalse();
            fixture.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
            ReconciliationV1 claim = (await new PayloadReconciliationClaims(fixture.Store, fixture.Clock).TryClaimAsync(new(Operation, fixture.Scope)))!.Value;
            await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.ReconcileAsync(fixture.Identity, claim).AsTask());
        }
        else { await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(fixture.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02").AsTask()); }
        await AssertRetainedAsync(fixture, write.Completion!, rotation);
        foreach (byte[] output in outputs) { output.ShouldAllBe(b => b == 0); }
        fixture.Backend.WrapCalls.ShouldBe(2);
    }

    [Theory]
    [InlineData(PayloadBackendFailure.Unavailable)]
    [InlineData(PayloadBackendFailure.Ambiguous)]
    public async Task UncertainReconciliationNeverRestoresOrTerminalizesTheOriginalRewrap(int failureValue)
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, _, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.Denied));
        var rotation = Rotation(f);
        await Should.ThrowAsync<PayloadProtectionStateException>(() => rotation.RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02").AsTask());
        f.Backend.Reconcile = (_, _) => ValueTask.FromResult(new PayloadBackendResult((PayloadBackendFailure)failureValue));
        var claim = (await new PayloadReconciliationClaims(f.Store, f.Clock).TryClaimAsync(new(Operation, f.Scope)))!.Value;
        (await rotation.ReconcileAsync(f.Identity, claim)).ShouldBeFalse();
        await AssertRetainedAsync(f, write.Completion!, rotation);
        (await f.Store.ReadAsync<OperationV1>(PayloadProtectionStateKeys.Operation(Operation)))!.Value.Result.ShouldBe("Pending");
        (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Operation)))!.Value.Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task ForegroundRewrapRetainsItsTargetAndCannotBeClaimedWhileTheWrapIsStillRunning()
    {
        ReservationTestFixture f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        f.Backend.Unwrap = (_, provider, _, _) => ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider));
        var response = new TaskCompletionSource<PayloadBackendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Backend.Wrap = (_, _, _) => new(response.Task);
        Task<bool> pending = Rotation(f).RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02").AsTask();
        var claims = new PayloadReconciliationClaims(f.Store, f.Clock);
        (await claims.TryClaimAsync(new(Operation, f.Scope))).ShouldBeNull();
        ReconciliationV1 intent = (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(Operation)))!.Value;
        intent.TargetProviderVersionDigest.ShouldBe(PayloadProtectionStateKeys.Digest("fixture:version-02"));
        intent.Status.ShouldBe("Pending"); intent.Attempt.ShouldBe(0u);
        (PayloadProtectionStateCodec.ParseTimestamp(intent.NotBeforeUtc) - f.Clock.GetUtcNow()).ShouldBe(TimeSpan.FromSeconds(90));
        response.SetResult(new(PayloadBackendFailure.None, new byte[32], "fixture:version-02"));
        (await pending).ShouldBeTrue(); f.Backend.WrapCalls.ShouldBe(2);
    }

    [Fact]
    public async Task ExactTargetIsPassedIntoWrapEvenWhenTheCurrentVersionChangesAfterIntent()
    {
        var f = await ReservationTestFixture.CreateAsync();
        var write = await f.Reservations.ProtectEventAsync(CompatibilityTestData.PlainJson(), ["/email"], TestFixture.Context());
        await f.Reservations.CompleteAsync(write.Completion!, PayloadProtectionPersistenceOutcome.NotPersisted);
        string current = "fixture:version-02"; bool wrapped = false;
        f.Backend.Unwrap = (_, provider, _, _) => { current = "fixture:version-03"; return ValueTask.FromResult(new PayloadBackendResult(PayloadBackendFailure.None, new byte[32], provider)); };
        f.Backend.WrapExact = async (operation, target, _, token) =>
        {
            current.ShouldBe("fixture:version-03"); target.ShouldBe("fixture:version-02");
            (await f.Store.ReadAsync<ReconciliationV1>(PayloadProtectionStateKeys.Reconciliation(operation), token))!.Value.TargetProviderVersionDigest.ShouldBe(PayloadProtectionStateKeys.Digest(target));
            wrapped = true; return new(PayloadBackendFailure.None, new byte[32], target);
        };
        (await Rotation(f).RewrapAsync(f.Identity, write.Completion!.KeyReference, 1, Operation, "fixture:version-02")).ShouldBeTrue();
        wrapped.ShouldBeTrue();
    }

    [Fact]
    public void RewrapTargetIsRequiredAndClosedWhileEveryOtherKindRequiresAnEmptyTarget()
    {
        string now = PayloadProtectionStateCodec.Timestamp(DateTimeOffset.UtcNow);
        var claim = new ReconciliationV1(Operation, new('a', 64), Operation, 1, "Rewrap", now, 0, "Pending", "none", now);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(claim));
        claim = claim with { TargetProviderVersionDigest = new('b', 64) };
        PayloadProtectionStateCodec.Decode<ReconciliationV1>(PayloadProtectionStateCodec.Encode(claim)).ShouldBe(claim);
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(claim with { Kind = "Create" }));
        Should.Throw<PayloadProtectionFormatException>(() => PayloadProtectionStateCodec.Encode(claim with { TargetProviderVersionDigest = new('B', 64) }));
    }

    private static PayloadKeyRotationCoordinator Rotation(ReservationTestFixture f) => new(f.Store, new([new("tenant-a", "parties")]), f.Leases, f.Backend, new(f.Clock), _ => true, "fixture-actor", f.Clock);
    private static async ValueTask AssertRetainedAsync(ReservationTestFixture f, PayloadProtectionCompletionContext completion, PayloadKeyRotationCoordinator rotation)
    {
        WrappedDekV1 key = (await f.Store.ReadAsync<WrappedDekV1>(PayloadProtectionStateKeys.Key(f.Scope, completion.KeyReference, 1)))!.Value;
        key.State.ShouldBe("RewrapPending"); key.ProviderKeyVersionId.ShouldBe("fixture:version-01");
        key.WrappedDek.ShouldBe(Base64UrlCodec.Encode(new byte[32]));
        PayloadKeyIndexes.VerifyMembership(key, await new PayloadKeyIndexes(f.Store, f.Clock).ReadScopeAsync(f.Scope), await rotation.VerifyInventoryAsync("fixture:version-01"));
        (await rotation.VerifyInventoryAsync("fixture:version-02")).Head.ShouldBeNull();
        (await new PayloadProtectionCatalogs(f.Store, f.Clock).ReadWorkAsync(f.Root)).Pages.SelectMany(p => p.Value.Entries).ShouldContain(e => e.OperationId == Operation);
    }
}
