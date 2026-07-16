using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionDeliveryIdempotencyCoordinatorTests {
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
