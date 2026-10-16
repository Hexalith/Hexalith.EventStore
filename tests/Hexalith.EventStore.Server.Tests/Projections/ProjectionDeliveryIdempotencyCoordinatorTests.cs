using System.Security.Cryptography;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Actors;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryIdempotencyCoordinatorTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedCurrent_DoubleSamplesSourceAndPerformsNoHistoryOrMutations(bool changeHead)
    {
        SourceProof seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        SourceProof head = SourceProofCodec.Extend(seed, new EventEnvelope("message-1", Identity.AggregateId, "Order", Identity.TenantId,
            Identity.Domain, 1, 1, Now, "correlation-1", "cause-1", "user-1", "v1", "OrderChanged", 1, "json", [1], null));
        string fingerprint = new('a', 64);
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, "single-event-v1", "projection-owner-proof/v1",
            "event-message-id/v1", "read-model-batch/v1");
        var entry = new NamedProjectionRouteCatalogEntry("sample", "v1", Identity.Domain, 2,
            ProjectionDispatchProtocol.Capability, fingerprint, ["order-detail"])
        {
            SourceGuardCapabilities = new Dictionary<string, ProjectionSourceGuardCapability> { ["order-detail"] = capability },
        };
        INamedProjectionRouteCatalog catalogs = Substitute.For<INamedProjectionRouteCatalog>();
        catalogs.Current.Returns(new NamedProjectionRouteCatalogSnapshot([entry]));
        var marker = new ProjectionSourceGuard(1, 1, 2, 2, 1, Identity.TenantId, Identity.Domain, Identity.AggregateId, "order-detail",
            seed.SourceIncarnation, fingerprint, capability.HandlerSemantics, ProjectionSourceGuard.ApprovedSpecSha256, new string('b', 40), Now);
        var selected = new ProjectionOwnerProof(new(1, Identity.TenantId, Identity.Domain, Identity.AggregateId, "order-detail",
            "owner-1", "generation-1", 1, "message-1", "message-1", "version-1"), "receipt-1", "v1:" + new string('A', 43));
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [Event(1)]);
        ProjectionDeliveryState state = EmptyState() with
        {
            SchemaVersion = 2, GuardEvidenceVersion = 1, LastDeliveredSequence = 1, LastCompletedMessageId = "message-1",
            CompletedPrefixFingerprint = history.PrefixFingerprint, CompletedReceipts = [Receipt(history.Events[0])], FirstRetainedSequence = 1,
            CompletedMessageIdDigests = [Convert.ToBase64String(SHA256.HashData("message-1"u8))],
            GuardEvidence = new(head, fingerprint, capability.HandlerSemantics, selected, 2),
        };
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>()).Returns(new ProjectionDeliveryStateReadResult(
            state, "etag", ProjectionDeliveryStateClassification.Current, true) { SourceGuard = marker });
        IAggregateActor source = Substitute.For<IAggregateActor>();
        var inspection = new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, head);
        source.InspectSourceProofAsync().Returns(inspection, changeHead ? inspection with { CurrentSequence = 2 } : inspection);
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(source);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        var idle = new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 2);
        lifecycle.ReadSnapshotAsync(Identity, "order-detail", Arg.Any<CancellationToken>()).Returns(idle);
        IProjectionSelectedOwnerProofGateway proof = Substitute.For<IProjectionSelectedOwnerProofGateway>();
        proof.ReadAsync(Identity, entry, "order-detail", idle, Arg.Any<CancellationToken>()).Returns(new ProjectionOwnerProofResult(selected, null, 4, 1024));
        var delivery = new ProjectionSourceGuardDelivery(catalogs, store, lifecycle, proof, actors, Options.Create(new EventStoreActorOptions()));

        ProjectionSourceGuardDeliveryResult result = await delivery.TryDeliverAsync(Identity, CancellationToken.None);

        result.Owned.ShouldBeTrue();
        result.AlreadyCurrent.ShouldBe(!changeHead);
        result.ReasonCode.ShouldBe(changeHead ? "head_changed" : null);
        await source.Received(2).InspectSourceProofAsync();
        await source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
        await source.DidNotReceiveWithAnyArgs().ReadEventsBoundedRangeAsync(default, default, default, default);
        await store.DidNotReceiveWithAnyArgs().TrySaveAsync(default!, default!, default!, default!, default);
        await lifecycle.DidNotReceiveWithAnyArgs().BeginDeliveryWriteAsync(default!, default!, default!, default);
        await lifecycle.DidNotReceiveWithAnyArgs().CompleteDeliveryWriteAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task GuardedCompletion_RequiresPendingFenceAndActualIdleBindingWhileCheckpointRemainsUnchanged()
    {
        SourceProof seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var stored = new EventEnvelope("message-1", Identity.AggregateId, "Order", Identity.TenantId, Identity.Domain,
            1, 1, Now, "correlation-1", "cause-1", "user-1", "v1", "OrderChanged", 1, "json", [1], null);
        SourceProof head = SourceProofCodec.Extend(seed, stored);
        string catalog = new('a', 64);
        var marker = new ProjectionSourceGuard(1, 1, 2, 2, 1, Identity.TenantId, Identity.Domain,
            Identity.AggregateId, "order-detail", seed.SourceIncarnation, catalog, "single-event-v1",
            ProjectionSourceGuard.ApprovedSpecSha256, new string('b', 40), Now);
        ProjectionDeliveryState state = EmptyState() with { SchemaVersion = 2, GuardEvidenceVersion = 1 };
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        int writes = 0;
        store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>()).Returns(_ =>
            new ProjectionDeliveryStateReadResult(state, "etag", ProjectionDeliveryStateClassification.Current, true) { SourceGuard = marker });
        store.TrySaveAsync(Identity, "order-detail", Arg.Any<ProjectionDeliveryState>(), "etag", Arg.Any<CancellationToken>()).Returns(call =>
        {
            state = call.ArgAt<ProjectionDeliveryState>(2);
            writes++;
            return true;
        });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [Event(1)]);
        var binding = new ProjectionDeliveryGuardReservation(seed, head, catalog, "single-event-v1", "generation-1", null);
        ProjectionDeliveryAdmissionResult admitted = await coordinator.TryAdmitHistoryAsync(Identity, "order-detail", history,
            true, guardBinding: binding);
        ProjectionDeliveryReservation reservation = admitted.Reservation.ShouldNotBeNull();
        var owner = new ProjectionOwnerProof(new(1, Identity.TenantId, Identity.Domain, Identity.AggregateId, "order-detail",
            "owner-1", "generation-1", 1, "message-1", "message-1", "version-1"), "receipt-1", "v1:" + new string('A', 43));
        var evidence = new ProjectionDeliveryGuardEvidence(head, catalog, "single-event-v1", owner, 2);

        (await coordinator.CompleteHistoryAsync(Identity, "order-detail", history, reservation, guardEvidence: evidence))
            .ShouldBe(ProjectionDeliveryCompletion.Fenced);
        (await coordinator.PrepareIdleBindingAsync(Identity, "order-detail", reservation, "operation-1", owner)).ShouldBeTrue();
        state.LastDeliveredSequence.ShouldBe(0);
        state.ActiveReservation.ShouldBe(reservation);
        state.IdleBindingPending.ShouldNotBeNull();
        (await coordinator.TryReleaseAsync(Identity, "order-detail", reservation)).ShouldBeFalse();
        (await coordinator.CompleteHistoryAsync(Identity, "order-detail", history, reservation,
            guardEvidence: evidence with { IdleLifecycleRevision = 0 })).ShouldBe(ProjectionDeliveryCompletion.Fenced);
        (await coordinator.CompleteHistoryAsync(Identity, "order-detail", history, reservation, guardEvidence: evidence))
            .ShouldBe(ProjectionDeliveryCompletion.Completed);
        state.LastDeliveredSequence.ShouldBe(1);
        state.IdleBindingPending.ShouldBeNull();
        state.ActiveReservation.ShouldBeNull();
        state.GuardEvidence.ShouldBe(evidence);
        state.CompletedMessageIdDigests.ShouldHaveSingleItem();
        writes.ShouldBe(3);
    }

    private static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AdmitEmpty_ConditionallyPersistsReservationBeforeDispatch() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(null, string.Empty, ProjectionDeliveryStateClassification.Absent, true));
        _ = store.TrySaveAsync(Identity, "order-detail", Arg.Any<ProjectionDeliveryState>(), string.Empty, Arg.Any<CancellationToken>())
            .Returns(true);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Dispatch);
        result.Reservation.ShouldNotBeNull();
        result.Reservation.DispatchId.ShouldBe("message-1");
        result.Reservation.FencingToken.ShouldBe(1);
        result.Reservation.ExpiresAt.ShouldBe(Now.AddMinutes(5));
        _ = await store.Received(1).TrySaveAsync(
            Identity,
            "order-detail",
            Arg.Is<ProjectionDeliveryState>(state =>
                state.LastDeliveredSequence == 0
                && state.ActiveReservation != null
                && state.ActiveReservation.HeadMessageId == "message-1"),
            string.Empty,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveExactDuplicate_IsRetryableWithoutStateMutation() {
        ProjectionDeliveryState state = EmptyState() with {
            ActiveReservation = Reservation(1, "message-1"),
        };
        IProjectionDeliveryStateStore store = StoreReturning(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryInProgress);
        _ = await store.DidNotReceiveWithAnyArgs().TrySaveAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task CompleteThenDuplicate_IsAlreadyCompletedAndDoesNotMutate() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        ProjectionDeliveryState reserved = EmptyState() with { ActiveReservation = Reservation(1, "message-1") };
        ProjectionDeliveryState? saved = null;
        int reads = 0;
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(_ => reads++ == 0
                ? new ProjectionDeliveryStateReadResult(reserved, "etag-1", ProjectionDeliveryStateClassification.Current, true)
                : new ProjectionDeliveryStateReadResult(saved, "etag-2", ProjectionDeliveryStateClassification.Current, true));
        _ = store.TrySaveAsync(Identity, "order-detail", Arg.Any<ProjectionDeliveryState>(), "etag-1", Arg.Any<CancellationToken>())
            .Returns(call => {
                saved = call.ArgAt<ProjectionDeliveryState>(2);
                return true;
            });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryCompletion completion = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reserved.ActiveReservation!);
        ProjectionDeliveryAdmissionResult duplicate = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        completion.ShouldBe(ProjectionDeliveryCompletion.Completed);
        saved.ShouldNotBeNull();
        saved.LastDeliveredSequence.ShouldBe(1);
        saved.ActiveReservation.ShouldBeNull();
        saved.CompletedReceipts!.Count.ShouldBe(1);
        duplicate.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.AlreadyCompleted);
        duplicate.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryAlreadyCompleted);
    }

    [Fact]
    public async Task RetainedIdentityConflict_FailsWithoutMutation() {
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [Event(1)]);
        ProjectionDeliveryEventDigest digest = history.Events[0];
        ProjectionDeliveryState state = EmptyState() with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = digest.MessageId,
            CompletedPrefixFingerprint = digest.PrefixFingerprint,
            CompletedReceipts = [new ProjectionDeliveryReceipt(1, digest.MessageId, digest.EventFingerprint, digest.PrefixFingerprint)],
            FirstRetainedSequence = 1,
        };
        IProjectionDeliveryStateStore store = StoreReturning(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1) with { MessageId = "different-message" }],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        _ = await store.DidNotReceiveWithAnyArgs().TrySaveAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task NonContiguousInput_IsGapWithoutStoreReadOrMutation() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1), Event(3)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        _ = await store.DidNotReceiveWithAnyArgs().ReadAsync(default!, default!, default);
    }

    [Fact]
    public async Task Completion_CompactsReceiptsDeterministically() {
        ProjectionDeliveryState state = EmptyState() with {
            ActiveReservation = new ProjectionDeliveryReservation(
                1, 3, "message-3", "message-3",
                ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [Event(1), Event(2), Event(3)]).PrefixFingerprint,
                1, Now, Now.AddMinutes(5), 1),
        };
        IProjectionDeliveryStateStore store = StoreReturning(state);
        ProjectionDeliveryState? saved = null;
        _ = store.TrySaveAsync(Identity, "order-detail", Arg.Any<ProjectionDeliveryState>(), "etag", Arg.Any<CancellationToken>())
            .Returns(call => {
                saved = call.ArgAt<ProjectionDeliveryState>(2);
                return true;
            });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store, receiptLimit: 2);

        ProjectionDeliveryCompletion result = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1), Event(2), Event(3)],
            state.ActiveReservation!);

        result.ShouldBe(ProjectionDeliveryCompletion.Completed);
        saved.ShouldNotBeNull();
        saved.CompletedReceipts!.Select(static receipt => receipt.SequenceNumber).ShouldBe([2, 3]);
        saved.FirstRetainedSequence.ShouldBe(2);
        saved.LastDeliveredSequence.ShouldBe(3);
    }

    [Fact]
    public async Task ActiveReservation_WithMatchingDurableFence_ReclaimsImmediatelyForRightfulOwner() {
        ProjectionDeliveryState state = EmptyState() with {
            ActiveReservation = Reservation(1, "message-1") with { FencingToken = 7 },
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        // The rightful owner presents the exact durable fence of its own unexpired reservation, so it
        // resumes immediately instead of waiting out the lease. The reclaim advances the fence so any
        // stale holder of token 7 is fenced out of a later completion.
        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true,
            resumeFencingToken: 7);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Dispatch);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryLeaseReclaimed);
        result.Reservation!.FencingToken.ShouldBe(8);
        result.Reservation.Attempt.ShouldBe(2);
        store.State!.ActiveReservation!.FencingToken.ShouldBe(8);
    }

    [Fact]
    public async Task ActiveReservation_WithNonMatchingFence_RemainsRetryableUntilLeaseExpiry() {
        ProjectionDeliveryState state = EmptyState() with {
            ActiveReservation = Reservation(1, "message-1") with { FencingToken = 7 },
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        // A caller that is not the current owner (stale or absent fence) must wait for the unexpired
        // lease to lapse before any reclaim, so it can never cut in front of the live owner.
        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true,
            resumeFencingToken: 6);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryInProgress);
        result.Reservation.ShouldBeNull();
        store.State.ShouldBe(state);
    }

    [Fact]
    public async Task ConditionalRetry_RefreshesReservationTimestamps() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(null, string.Empty, ProjectionDeliveryStateClassification.Absent, true));
        var timeProvider = new FakeTimeProvider(Now);
        var attemptedStates = new List<ProjectionDeliveryState>();
        _ = store.TrySaveAsync(Identity, "order-detail", Arg.Any<ProjectionDeliveryState>(), string.Empty, Arg.Any<CancellationToken>())
            .Returns(call => {
                attemptedStates.Add(call.ArgAt<ProjectionDeliveryState>(2));
                if (attemptedStates.Count == 1) {
                    timeProvider.Advance(TimeSpan.FromMinutes(1));
                    return false;
                }

                return true;
            });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store, timeProvider: timeProvider);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Dispatch);
        attemptedStates.Count.ShouldBe(2);
        attemptedStates[1].ActiveReservation!.AdmittedAt.ShouldBe(Now.AddMinutes(1));
        attemptedStates[1].ActiveReservation.ExpiresAt.ShouldBe(Now.AddMinutes(6));
        attemptedStates[1].UpdatedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public async Task ExpiredReservation_ReclaimsSameIdentityAndFencesLateCompletion() {
        ProjectionDeliveryReservation expired = Reservation(1, "message-1") with {
            AdmittedAt = Now.AddMinutes(-5),
            ExpiresAt = Now.AddTicks(-1),
        };
        var store = new InMemoryProjectionDeliveryStateStore(EmptyState() with { ActiveReservation = expired });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult reclaimed = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);
        ProjectionDeliveryCompletion stale = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1)],
            expired);
        ProjectionDeliveryCompletion current = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimed.Reservation!);

        reclaimed.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Dispatch);
        reclaimed.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryLeaseReclaimed);
        reclaimed.Reservation!.FencingToken.ShouldBe(2);
        reclaimed.Reservation.Attempt.ShouldBe(2);
        stale.ShouldBe(ProjectionDeliveryCompletion.Fenced);
        current.ShouldBe(ProjectionDeliveryCompletion.Completed);
        store.State!.LastDeliveredSequence.ShouldBe(1);
    }

    [Fact]
    public async Task ExpiredReservation_WithoutReplaySafeRoute_RecordsReconciliation() {
        ProjectionDeliveryReservation expired = Reservation(1, "message-1") with {
            AdmittedAt = Now.AddMinutes(-5),
            ExpiresAt = Now.AddTicks(-1),
        };
        var store = new InMemoryProjectionDeliveryStateStore(EmptyState() with { ActiveReservation = expired });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: false);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        store.State!.ActiveReservation.ShouldBe(expired);
        store.ReconciliationWork.ShouldHaveSingleItem().ReasonCode
            .ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
    }

    [Fact]
    public async Task ExhaustedConditionalConflicts_AreRetryableWithoutReservation() {
        var store = new InMemoryProjectionDeliveryStateStore { SaveFailuresRemaining = 3 };
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(
            store,
            maxStateTransitionAttempts: 3);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Retryable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryStateUnavailable);
        store.State.ShouldBeNull();
    }

    [Fact]
    public async Task CompletionExhaustsConditionalConflicts_PreservesReservationForRetry() {
        ProjectionDeliveryReservation reservation = Reservation(1, "message-1");
        ProjectionDeliveryState reserved = EmptyState() with { ActiveReservation = reservation };
        var store = new InMemoryProjectionDeliveryStateStore(reserved) { SaveFailuresRemaining = 3 };
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(
            store,
            maxStateTransitionAttempts: 3);

        ProjectionDeliveryCompletion result = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reservation);

        result.ShouldBe(ProjectionDeliveryCompletion.StateUnavailable);
        store.State.ShouldBe(reserved);
        store.State!.ActiveReservation.ShouldBe(reservation);
        store.State.LastDeliveredSequence.ShouldBe(0);
    }

    [Fact]
    public async Task MalformedRetainedReceipt_FailsClosedWithoutIndexingHistory() {
        ProjectionDeliveryState malformed = EmptyState() with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = "message-1",
            CompletedPrefixFingerprint = "v1:prefix",
            CompletedReceipts = [new ProjectionDeliveryReceipt(0, "message-1", "v1:event", "v1:prefix")],
            FirstRetainedSequence = 0,
        };
        var store = new InMemoryProjectionDeliveryStateStore(malformed);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliverySchemaRegression);
        store.State.ShouldBe(malformed);
    }

    [Fact]
    public async Task MalformedReservation_CannotResumeWithMatchingFence() {
        ProjectionDeliveryReservation reservation = Reservation(1, "message-1") with {
            FencingToken = 0,
        };
        ProjectionDeliveryState malformed = EmptyState() with { ActiveReservation = reservation };
        var store = new InMemoryProjectionDeliveryStateStore(malformed);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true,
            resumeFencingToken: 0);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliverySchemaRegression);
        store.State.ShouldBe(malformed);
    }

    [Fact]
    public async Task ExactCompletedReceiptInMalformedState_IsFencedBeforeDuplicateClassification() {
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [Event(1)]);
        ProjectionDeliveryEventDigest digest = history.Events[0];
        ProjectionDeliveryState malformed = EmptyState() with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = digest.MessageId,
            CompletedPrefixFingerprint = digest.PrefixFingerprint,
            CompletedReceipts = [Receipt(digest)],
            FirstRetainedSequence = 0,
        };
        var store = new InMemoryProjectionDeliveryStateStore(malformed);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryCompletion result = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            [Event(1)],
            Reservation(1, "message-1"));

        result.ShouldBe(ProjectionDeliveryCompletion.Fenced);
        store.State.ShouldBe(malformed);
    }

    [Fact]
    public async Task ConcurrentClaims_ProduceOneReservationAndOneDeferredDuplicate() {
        var store = new InMemoryProjectionDeliveryStateStore();
        ProjectionDeliveryIdempotencyCoordinator first = CreateCoordinator(store);
        ProjectionDeliveryIdempotencyCoordinator second = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult[] results = await Task.WhenAll(
            first.TryAdmitAsync(Identity, "order-detail", [Event(1)], reclaimSafe: true),
            second.TryAdmitAsync(Identity, "order-detail", [Event(1)], reclaimSafe: true));

        results.Count(result => result.Disposition == ProjectionDeliveryAdmissionDisposition.Dispatch).ShouldBe(1);
        results.Count(result => result.Disposition == ProjectionDeliveryAdmissionDisposition.Retryable).ShouldBe(1);
        store.State!.ActiveReservation.ShouldNotBeNull();
    }

    [Fact]
    public async Task BelowRetentionFloor_RecordsReconciliationAndPreservesState() {
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            [Event(1), Event(2), Event(3)]);
        ProjectionDeliveryState state = EmptyState() with {
            LastDeliveredSequence = 3,
            LastCompletedMessageId = "message-3",
            CompletedPrefixFingerprint = history.Events[2].PrefixFingerprint,
            CompletedReceipts = [
                Receipt(history.Events[1]),
                Receipt(history.Events[2]),
            ],
            FirstRetainedSequence = 2,
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitAsync(
            Identity,
            "order-detail",
            [Event(1)],
            reclaimSafe: true);

        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        store.State.ShouldBe(state);
        store.ReconciliationWork.ShouldHaveSingleItem().ObservedSequence.ShouldBe(1);
    }

    [Fact]
    public async Task MappedV2CompletedReceiptRejectsPositionAndProvenanceOnlyRedelivery() {
        ProjectionEventDto source = Event(1) with { GlobalPosition = 19 };
        ProjectionDeliveryFingerprintHistory original = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [source], positionSensitive: true);
        ProjectionDeliveryEventDigest completed = original.Events[0];
        ProjectionDeliveryState state = ProjectionDeliveryState.CreateEmpty(
            Identity, "order-detail", original.InitialFingerprint, Now) with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = source.MessageId,
            CompletedPrefixFingerprint = completed.PrefixFingerprint,
            CompletedReceipts = [Receipt(completed)],
            FirstRetainedSequence = 1,
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        (await coordinator.ReadAcceptedPrefixAsync(Identity, "order-detail")).ShouldBeNull();

        (await coordinator.UseLegacyMappedIdentityAsync(Identity, "order-detail")).ShouldBeFalse();
        foreach (ProjectionEventDto changed in new[] {
            source with { GlobalPosition = 20 },
            source with { StoredEventContractType = "stored-v2" },
            source with { RegistryFingerprint = "registry-v2" },
        }) {
            ProjectionDeliveryFingerprintHistory variant = ProjectionDeliveryFingerprint.ComputeHistory(
                Identity, "order-detail", [changed], positionSensitive: true);
            ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitHistoryAsync(
                Identity, "order-detail", variant, reclaimSafe: true);
            result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
            result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
            store.State.ShouldBe(state);
        }
    }

    [Fact]
    public async Task ExistingMappedV1ReceiptKeepsItsLegacyIdentity() {
        ProjectionEventDto source = Event(1) with { GlobalPosition = 19 };
        ProjectionDeliveryFingerprintHistory legacy = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [source]);
        ProjectionDeliveryEventDigest completed = legacy.Events[0];
        ProjectionDeliveryState state = EmptyState() with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = source.MessageId,
            CompletedPrefixFingerprint = completed.PrefixFingerprint,
            CompletedReceipts = [Receipt(completed)],
            FirstRetainedSequence = 1,
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        (await coordinator.UseLegacyMappedIdentityAsync(Identity, "order-detail")).ShouldBeTrue();
        ProjectionDeliveryFingerprintHistory replay = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [source with { GlobalPosition = 20 }]);
        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitHistoryAsync(
            Identity, "order-detail", replay, reclaimSafe: true);
        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.AlreadyCompleted);
        store.State.ShouldBe(state);
    }

    [Fact]
    public async Task AnchoredSuffixAdvancesDurablePrefixWithoutReplayingPriorEvents() {
        ProjectionEventDto first = Event(1) with { GlobalPosition = 19 };
        ProjectionEventDto second = Event(2) with { GlobalPosition = 20 };
        ProjectionDeliveryFingerprintHistory original = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [first], positionSensitive: true);
        ProjectionDeliveryEventDigest completed = original.Events[0];
        ProjectionDeliveryState state = ProjectionDeliveryState.CreateEmpty(Identity, "order-detail",
            original.InitialFingerprint, Now) with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = first.MessageId,
            CompletedPrefixFingerprint = completed.PrefixFingerprint,
            CompletedReceipts = [Receipt(completed)],
            CompletedMessageIdDigests = [Convert.ToBase64String(SHA256.HashData(
                Encoding.UTF8.GetBytes(first.MessageId)))],
            FirstRetainedSequence = 1,
        };
        var store = new InMemoryProjectionDeliveryStateStore(state);
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);
        ProjectionDeliveryAcceptedPrefix checkpoint = (await coordinator.ReadAcceptedPrefixAsync(
            Identity, "order-detail")).ShouldNotBeNull();
        checkpoint.SequenceNumber.ShouldBe(1);
        async IAsyncEnumerable<ProjectionEventDto[]> Suffix() {
            yield return [second];
            await Task.Yield();
        }
        ProjectionDeliveryFingerprintHistory history = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", checkpoint.SequenceNumber, checkpoint.Fingerprint,
            Suffix(), 10_000, CancellationToken.None);

        ProjectionDeliveryAdmissionResult admitted = await coordinator.TryAdmitHistoryAsync(
            Identity, "order-detail", history, reclaimSafe: true);
        admitted.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Dispatch);
        (await coordinator.CompleteHistoryAsync(Identity, "order-detail", history,
            admitted.Reservation.ShouldNotBeNull())).ShouldBe(ProjectionDeliveryCompletion.Completed);
        store.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(2);
        store.State.CompletedPrefixFingerprint.ShouldBe(history.PrefixFingerprint);

        ProjectionDeliveryFingerprintHistory changed = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", checkpoint.SequenceNumber, checkpoint.Fingerprint,
            Changed(), 10_000, CancellationToken.None);
        (await coordinator.TryAdmitHistoryAsync(Identity, "order-detail", changed,
            reclaimSafe: true)).Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        async IAsyncEnumerable<ProjectionEventDto[]> Changed() {
            yield return [second with { GlobalPosition = 21 }];
            await Task.Yield();
        }
    }

    [Fact]
    public async Task IncrementalSuffixEnforcesTotalOwnerEventLimit() {
        string prefix = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [Event(1)]).PrefixFingerprint;
        async IAsyncEnumerable<ProjectionEventDto[]> OneEvent(long sequence) {
            yield return [Event(sequence)];
            await Task.Yield();
        }

        ProjectionDeliveryFingerprintHistory accepted = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", 9_999, prefix, OneEvent(10_000), 10_000,
            CancellationToken.None);
        accepted.Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(10_000);
        _ = await Should.ThrowAsync<ArgumentException>(() => ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", 10_000, prefix, OneEvent(10_001), 10_000,
            CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(() => ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", 9_999, prefix, TwoEvents(), 10_000,
            CancellationToken.None));
        ProjectionDeliveryFingerprintHistory completeOverLimit = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", [.. Enumerable.Range(1, 10_001).Select(sequence => Event(sequence))]);
        ProjectionDeliveryFingerprint.IsValidHistory(Identity, "order-detail", completeOverLimit)
            .ShouldBeFalse();

        async IAsyncEnumerable<ProjectionEventDto[]> TwoEvents() {
            yield return [Event(10_000), Event(10_001)];
            await Task.Yield();
        }
    }

    [Theory]
    [InlineData(256)]
    [InlineData(4096)]
    public async Task Compaction_RespectsConfiguredBoundaryAndKeepsCumulativeHead(int limit) {
        ProjectionEventDto[] events = [.. Enumerable.Range(1, limit + 1).Select(sequence => Event(sequence))];
        ProjectionDeliveryFingerprintHistory history = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity,
            "order-detail",
            events);
        ProjectionDeliveryReservation reservation = new(
            1,
            events.Length,
            events[^1].MessageId,
            events[^1].MessageId,
            history.PrefixFingerprint,
            1,
            Now,
            Now.AddMinutes(5),
            1);
        var store = new InMemoryProjectionDeliveryStateStore(EmptyState() with { ActiveReservation = reservation });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store, receiptLimit: limit);

        ProjectionDeliveryCompletion result = await coordinator.CompleteAsync(
            Identity,
            "order-detail",
            events,
            reservation);

        result.ShouldBe(ProjectionDeliveryCompletion.Completed);
        store.State!.CompletedReceipts!.Count.ShouldBe(limit);
        store.State.FirstRetainedSequence.ShouldBe(2);
        store.State.CompletedPrefixFingerprint.ShouldBe(history.PrefixFingerprint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncrementalSuffixRejectsMessageIdOlderThanReceiptWindow(bool positionSensitive) {
        ProjectionEventDto[] first = [Event(1), Event(2)];
        ProjectionDeliveryFingerprintHistory initial = ProjectionDeliveryFingerprint.ComputeHistory(
            Identity, "order-detail", first, positionSensitive);
        ProjectionDeliveryReservation reservation = new(1, 2, first[^1].MessageId,
            first[^1].MessageId, initial.PrefixFingerprint, 1, Now, Now.AddMinutes(5), 1);
        var store = new InMemoryProjectionDeliveryStateStore(
            EmptyState() with { ActiveReservation = reservation,
                CompletedPrefixFingerprint = initial.InitialFingerprint });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store, receiptLimit: 1);
        (await coordinator.CompleteHistoryAsync(Identity, "order-detail", initial, reservation))
            .ShouldBe(ProjectionDeliveryCompletion.Completed);
        store.State!.CompletedReceipts!.Count.ShouldBe(1);
        (await coordinator.ReadAcceptedPrefixAsync(Identity, "order-detail")).ShouldNotBeNull();

        ProjectionEventDto reused = Event(3) with { MessageId = first[0].MessageId };
        async IAsyncEnumerable<ProjectionEventDto[]> Suffix() {
            yield return [reused];
            await Task.Yield();
        }
        ProjectionDeliveryFingerprintHistory suffix = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(
            Identity, "order-detail", 2, initial.PrefixFingerprint, Suffix(), 10_000,
            CancellationToken.None);
        ProjectionDeliveryAdmissionResult result = await coordinator.TryAdmitHistoryAsync(
            Identity, "order-detail", suffix, reclaimSafe: true);
        result.Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    [Fact]
    public async Task Release_RequiresExactFenceAndNeverAdvancesCheckpoint() {
        ProjectionDeliveryReservation reservation = Reservation(1, "message-1");
        var store = new InMemoryProjectionDeliveryStateStore(EmptyState() with { ActiveReservation = reservation });
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store);

        bool stale = await coordinator.TryReleaseAsync(
            Identity,
            "order-detail",
            reservation with { FencingToken = 2 });
        bool released = await coordinator.TryReleaseAsync(Identity, "order-detail", reservation);

        stale.ShouldBeFalse();
        released.ShouldBeTrue();
        store.State!.LastDeliveredSequence.ShouldBe(0);
        store.State.ActiveReservation.ShouldBeNull();
    }

    private static ProjectionDeliveryIdempotencyCoordinator CreateCoordinator(
        IProjectionDeliveryStateStore store,
        int receiptLimit = 256,
        int maxStateTransitionAttempts = 8,
        TimeProvider? timeProvider = null) => new(
            store,
            Options.Create(new ProjectionDeliveryIdempotencyOptions {
                CompletedReceiptLimit = receiptLimit,
                MaxStateTransitionAttempts = maxStateTransitionAttempts,
            }),
            timeProvider ?? new FakeTimeProvider(Now));

    private static IProjectionDeliveryStateStore StoreReturning(ProjectionDeliveryState state) {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(state, "etag", ProjectionDeliveryStateClassification.Current, true));
        return store;
    }

    private static ProjectionDeliveryState EmptyState() => ProjectionDeliveryState.CreateEmpty(
        Identity,
        "order-detail",
        ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-detail"),
        Now);

    private static ProjectionDeliveryReservation Reservation(long sequence, string messageId) => new(
        sequence,
        sequence,
        messageId,
        messageId,
        ProjectionDeliveryFingerprint.ComputeHistory(Identity, "order-detail", [Event(sequence)]).PrefixFingerprint,
        1,
        Now,
        Now.AddMinutes(5),
        1);

    private static ProjectionDeliveryReceipt Receipt(ProjectionDeliveryEventDigest digest) => new(
        digest.SequenceNumber,
        digest.MessageId,
        digest.EventFingerprint,
        digest.PrefixFingerprint);

    private static ProjectionEventDto Event(long sequence) => new(
        "OrderChanged",
        [(byte)sequence],
        "json",
        sequence,
        Now.AddMinutes(sequence),
        "correlation-1",
        $"message-{sequence}",
        "user-1");
}
