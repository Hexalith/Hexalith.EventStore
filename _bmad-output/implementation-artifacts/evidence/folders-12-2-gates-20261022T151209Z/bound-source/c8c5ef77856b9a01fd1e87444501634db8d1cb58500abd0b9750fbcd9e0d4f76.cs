using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryIdempotencyCoordinatorTests
{
    [Fact]
    public async Task GuardedFinalization_HungSourceReadStopsAtOriginalDeadlineAndRetainsPendingCompletion()
    {
        var fixture = new GuardedFinalizationFixture();
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        var neverReturns = new TaskCompletionSource<SourceProofInspection>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Source.InspectSourceProofAsync().Returns(neverReturns.Task);
        Task<ProjectionSourceGuardDeliveryResult> attempt = fixture.Finalizer.FinalizeAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.History, reservation, "own-token", fixture.Selected,
            CancellationToken.None);
        fixture.Released.ShouldBeTrue();
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        await Should.ThrowAsync<OperationCanceledException>(() => attempt.WaitAsync(TimeSpan.FromSeconds(10))).ConfigureAwait(true);
        fixture.State.LastDeliveredSequence.ShouldBe(0);
        fixture.State.ActiveReservation.ShouldBe(reservation);
        fixture.State.IdleBindingPending.ShouldNotBeNull();
        fixture.Writes.ShouldBe(2);
        _ = fixture.Source.Received(1).InspectSourceProofAsync();
    }

    [Fact]
    public async Task GuardedReservation_ProofLimitRejectionRemainsTypedAndDoesNotExhaustCASRetries()
    {
        var fixture = new GuardedFinalizationFixture();
        fixture.Store.TrySaveAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded"));
        ProjectionDeliveryHistoryValidationException error = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(
            () => fixture.ReserveAsync()).ConfigureAwait(true);
        error.Message.ShouldBe("delivery_proof_limit_exceeded");
        await fixture.Store.Received(1).TrySaveAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
        fixture.State.LastDeliveredSequence.ShouldBe(0);
        fixture.State.ActiveReservation.ShouldBeNull();
    }

    [Fact]
    public async Task GuardedFinalization_BindsActualIdleRevisionAfterRelease()
    {
        var fixture = new GuardedFinalizationFixture();
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        ProjectionSourceGuardDeliveryResult result = await fixture.Finalizer.FinalizeAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.History, reservation, "own-token", fixture.Selected,
            CancellationToken.None).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeTrue();
        fixture.Released.ShouldBeTrue();
        fixture.State.GuardEvidence.ShouldNotBeNull().IdleLifecycleRevision.ShouldBe(41);
        fixture.State.LastDeliveredSequence.ShouldBe(1);
        fixture.State.IdleBindingPending.ShouldBeNull();
        fixture.Writes.ShouldBe(3);
        await fixture.Source.DidNotReceiveWithAnyArgs().ReadMeasuredEventsBoundedRangeAsync(default, default, default, default).ConfigureAwait(true);
    }

    [Fact]
    public async Task GuardedFinalization_LostCompletionResponseIsResolvedByPersistedReadback()
    {
        var fixture = new GuardedFinalizationFixture { LoseCompletionResponse = true };
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        ProjectionSourceGuardDeliveryResult result = await fixture.Finalizer.FinalizeAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.History, reservation, "own-token", fixture.Selected,
            CancellationToken.None).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeTrue();
        fixture.State.LastDeliveredSequence.ShouldBe(1);
        fixture.Writes.ShouldBe(3);
    }

    [Fact]
    public async Task GuardedFinalization_ChangedLifecycleAfterCompletionCannotReportCurrent()
    {
        var fixture = new GuardedFinalizationFixture { ChangeLifecycleAfterCompletion = true };
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        ProjectionSourceGuardDeliveryResult result = await fixture.Finalizer.FinalizeAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.History, reservation, "own-token", fixture.Selected,
            CancellationToken.None).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("head_changed");
        fixture.State.LastDeliveredSequence.ShouldBe(1);
    }

    [Fact]
    public async Task GuardedFinalization_PendingReclaimRequiresRightfulOrExpiredFenceAndKeepsOriginalWork()
    {
        var fixture = new GuardedFinalizationFixture();
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        (await fixture.Coordinator.PrepareIdleBindingAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            reservation, "crashed-token", fixture.Selected).ConfigureAwait(true)).ShouldBeTrue();
        (await fixture.Coordinator.TryReclaimIdleBindingAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            reservation, "loser-token").ConfigureAwait(true)).ShouldBeNull();
        ProjectionDeliveryReservation recovered = (await fixture.Coordinator.TryReclaimIdleBindingAsync(GuardedFinalizationFixture.Identity,
            GuardedFinalizationFixture.Projection, reservation, "own-token", resumeFencingToken: reservation.FencingToken).ConfigureAwait(true)).ShouldNotBeNull();
        recovered.FencingToken.ShouldBe(reservation.FencingToken + 1);
        recovered.DispatchId.ShouldBe(reservation.DispatchId);
        recovered.ManifestFingerprint.ShouldBe(reservation.ManifestFingerprint);
        recovered.GuardBinding.ShouldBe(reservation.GuardBinding);
        fixture.State.LastDeliveredSequence.ShouldBe(0);
        fixture.State.IdleBindingPending.ShouldNotBeNull().LifecycleOperationToken.ShouldBe("own-token");
        (await fixture.Coordinator.TryReleaseAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            reservation).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task GuardedFinalization_ExpiredPendingRecoveryReadsFrozenTailAndCompletesWithoutRedispatch()
    {
        var fixture = new GuardedFinalizationFixture();
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        (await fixture.Coordinator.PrepareIdleBindingAsync(GuardedFinalizationFixture.Identity, GuardedFinalizationFixture.Projection,
            reservation, "crashed-token", fixture.Selected).ConfigureAwait(true)).ShouldBeTrue();
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        ProjectionSourceGuardDeliveryResult result = await fixture.Finalizer.RecoverAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.State, 1, CancellationToken.None).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeTrue();
        fixture.State.LastDeliveredSequence.ShouldBe(1);
        fixture.State.GuardEvidence.ShouldNotBeNull().IdleLifecycleRevision.ShouldBe(41);
        fixture.Writes.ShouldBe(4);
        await fixture.Lifecycle.DidNotReceive().CompleteDeliveryWriteAsync(GuardedFinalizationFixture.Identity,
            GuardedFinalizationFixture.Projection, "crashed-token", Arg.Any<CancellationToken>()).ConfigureAwait(true);
        await fixture.Source.Received(1).ReadMeasuredEventsBoundedRangeAsync(0, 1, 1, 2_097_152).ConfigureAwait(true);
    }

    [Fact]
    public async Task GuardedFinalization_MissingSelectedReceiptPreservesPendingAndDoesNotAdvance()
    {
        var fixture = new GuardedFinalizationFixture();
        ProjectionDeliveryReservation reservation = await fixture.ReserveAsync().ConfigureAwait(true);
        fixture.Owners.ReadAsync(GuardedFinalizationFixture.Identity, fixture.Catalog, GuardedFinalizationFixture.Projection,
            Arg.Any<ProjectionLifecycleSnapshot>(), Arg.Any<CancellationToken>()).Returns(new Hexalith.EventStore.Contracts.Projections.ProjectionOwnerProofResult(
                null, "delivery_reconciliation_required", 4, 0));
        ProjectionSourceGuardDeliveryResult result = await fixture.Finalizer.FinalizeAsync(GuardedFinalizationFixture.Identity,
            fixture.Catalog, GuardedFinalizationFixture.Projection, fixture.History, reservation, "own-token", fixture.Selected,
            CancellationToken.None).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        fixture.State.LastDeliveredSequence.ShouldBe(0);
        fixture.State.IdleBindingPending.ShouldNotBeNull();
        fixture.State.ActiveReservation.ShouldBe(reservation);
        fixture.Writes.ShouldBe(2);
    }
}
