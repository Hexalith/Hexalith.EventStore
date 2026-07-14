using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class ProjectionDeliveryReconcilerTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NonZeroLegacy_HydratesReceiptsWithoutAdvancingCheckpoint() {
        ProjectionDeliveryState legacy = LegacyState(2);
        IProjectionDeliveryStateStore store = StoreReturning(legacy, ProjectionDeliveryStateClassification.LegacyNonZero);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 2, Arg.Any<CancellationToken>()).Returns([Event(1), Event(2)]);
        ProjectionDeliveryState? saved = null;
        ProjectionDeliveryReconciliationWork? savedWork = null;
        _ = store.TrySaveWithReconciliationAsync(
                Identity,
                "order-detail",
                Arg.Any<ProjectionDeliveryState>(),
                Arg.Any<ProjectionDeliveryReconciliationWork>(),
                "etag",
                Arg.Any<CancellationToken>())
            .Returns(call => {
                saved = call.ArgAt<ProjectionDeliveryState>(2);
                savedWork = call.ArgAt<ProjectionDeliveryReconciliationWork>(3);
                return true;
            });
        var reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.Completed);
        result.PreservedSequence.ShouldBe(2);
        saved.ShouldNotBeNull();
        saved.LastDeliveredSequence.ShouldBe(2);
        saved.MigrationProvenance.ShouldBe(ProjectionDeliveryMigrationProvenance.HydratedFromPersistedCheckpoint);
        saved.CompletedReceipts!.Count.ShouldBe(2);
        savedWork.ShouldNotBeNull();
        savedWork.OperatorId.ShouldBe("operator-7");
        savedWork.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciled);
        savedWork.RecordedAt.ShouldBe(saved.UpdatedAt);
        await store.DidNotReceiveWithAnyArgs().RecordReconciliationAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task WrongPersistedScope_IsDeniedBeforeHistoryOrMutation() {
        ProjectionDeliveryState wrongTenant = LegacyState(2) with { TenantId = "tenant-b" };
        IProjectionDeliveryStateStore store = StoreReturning(wrongTenant, ProjectionDeliveryStateClassification.LegacyNonZero);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        var reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.ScopeDenied);
        _ = await history.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default);
        _ = await store.DidNotReceiveWithAnyArgs()
            .TrySaveWithReconciliationAsync(default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task CheckpointAheadOfEventStore_RecordsRebuildRequiredWithoutChangingRow() {
        ProjectionDeliveryState legacy = LegacyState(3);
        IProjectionDeliveryStateStore store = StoreReturning(legacy, ProjectionDeliveryStateClassification.LegacyNonZero);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 3, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ProjectionEventDto>>(_ =>
                throw new ProjectionDeliveryHistoryValidationException("short"));
        var reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.RebuildRequired);
        _ = await store.DidNotReceiveWithAnyArgs()
            .TrySaveWithReconciliationAsync(default!, default!, default!, default!, default!, default);
        await store.Received(1).RecordReconciliationAsync(
            Identity,
            "order-detail",
            Arg.Is<ProjectionDeliveryReconciliationWork>(work =>
                work.ReasonCode == ProjectionDispatchReasonCodes.DeliveryRebuildRequired
                && work.ObservedSequence == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbsentCheckpoint_InitializesZeroWithoutReadingOrInvokingHandlers() {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(
                null,
                string.Empty,
                ProjectionDeliveryStateClassification.Absent,
                true));
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 0, Arg.Any<CancellationToken>()).Returns([]);
        ProjectionDeliveryState? saved = null;
        _ = store.TrySaveWithReconciliationAsync(
                Identity,
                "order-detail",
                Arg.Any<ProjectionDeliveryState>(),
                Arg.Any<ProjectionDeliveryReconciliationWork>(),
                string.Empty,
                Arg.Any<CancellationToken>())
            .Returns(call => {
                saved = call.ArgAt<ProjectionDeliveryState>(2);
                return true;
            });
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.Completed);
        result.PreservedSequence.ShouldBe(0);
        saved.ShouldNotBeNull();
        saved.LastDeliveredSequence.ShouldBe(0);
        saved.MigrationProvenance.ShouldBe(ProjectionDeliveryMigrationProvenance.InitializedFromZero);
    }

    [Fact]
    public async Task CurrentPrefixMismatch_RecordsRebuildRequiredWithoutRehydratingReceipts() {
        ProjectionDeliveryState current = ProjectionDeliveryState.CreateEmpty(
            Identity,
            "order-detail",
            ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-detail"),
            Now) with {
            LastDeliveredSequence = 1,
            LastCompletedMessageId = "message-1",
            CompletedPrefixFingerprint = "v1:wrong-prefix",
            CompletedReceipts = [new ProjectionDeliveryReceipt(1, "message-1", "v1:wrong-event", "v1:wrong-prefix")],
            FirstRetainedSequence = 1,
        };
        IProjectionDeliveryStateStore store = StoreReturning(current, ProjectionDeliveryStateClassification.Current);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 1, Arg.Any<CancellationToken>()).Returns([Event(1)]);
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.RebuildRequired);
        _ = await store.DidNotReceiveWithAnyArgs()
            .TrySaveWithReconciliationAsync(default!, default!, default!, default!, default!, default);
        await store.Received(1).RecordReconciliationAsync(
            Identity,
            "order-detail",
            Arg.Is<ProjectionDeliveryReconciliationWork>(work =>
                work.OperatorId == "operator-7"
                && work.ReasonCode == ProjectionDispatchReasonCodes.DeliveryRebuildRequired),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveReservation_IsPreservedWithoutReadingHistory() {
        ProjectionDeliveryState current = ProjectionDeliveryState.CreateEmpty(
            Identity,
            "order-detail",
            ProjectionDeliveryFingerprint.ComputeInitial(Identity, "order-detail"),
            Now) with {
            ActiveReservation = new ProjectionDeliveryReservation(
                1,
                1,
                "message-1",
                "message-1",
                "v1:manifest",
                1,
                Now,
                Now.AddMinutes(5),
                1),
        };
        IProjectionDeliveryStateStore store = StoreReturning(current, ProjectionDeliveryStateClassification.Current);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.StateUnavailable);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryInProgress);
        _ = await history.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default);
        _ = await store.DidNotReceiveWithAnyArgs()
            .TrySaveWithReconciliationAsync(default!, default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task UnsafeSchemaClassification_RecordsRebuildWithoutOverwritingRow(
        int classification) {
        ProjectionDeliveryState observed = LegacyState(2);
        IProjectionDeliveryStateStore store = StoreReturning(
            observed,
            (ProjectionDeliveryStateClassification)classification);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.RebuildRequired);
        result.PreservedSequence.ShouldBe(2);
        _ = await history.DidNotReceiveWithAnyArgs().ReadAsync(default!, default, default);
        _ = await store.DidNotReceiveWithAnyArgs()
            .TrySaveWithReconciliationAsync(default!, default!, default!, default!, default!, default);
        await store.Received(1).RecordReconciliationAsync(
            Identity,
            "order-detail",
            Arg.Is<ProjectionDeliveryReconciliationWork>(work =>
                work.ReasonCode == ProjectionDispatchReasonCodes.DeliveryRebuildRequired),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransientHistoryFailure_RemainsStateUnavailableWithoutRebuildEvidence() {
        ProjectionDeliveryState legacy = LegacyState(2);
        IProjectionDeliveryStateStore store = StoreReturning(legacy, ProjectionDeliveryStateClassification.LegacyNonZero);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 2, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ProjectionEventDto>>(_ => throw new IOException("actor transport unavailable"));
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.StateUnavailable);
        result.PreservedSequence.ShouldBe(2);
        await store.DidNotReceiveWithAnyArgs().RecordReconciliationAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task ExhaustedHydrationConflicts_ReportLastObservedSequence() {
        ProjectionDeliveryState legacy = LegacyState(2);
        IProjectionDeliveryStateStore store = StoreReturning(legacy, ProjectionDeliveryStateClassification.LegacyNonZero);
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        _ = history.ReadAsync(Identity, 2, Arg.Any<CancellationToken>()).Returns([Event(1), Event(2)]);
        _ = store.TrySaveWithReconciliationAsync(
                Identity,
                "order-detail",
                Arg.Any<ProjectionDeliveryState>(),
                Arg.Any<ProjectionDeliveryReconciliationWork>(),
                "etag",
                Arg.Any<CancellationToken>())
            .Returns(false);
        ProjectionDeliveryReconciler reconciler = CreateReconciler(store, history, maxStateTransitionAttempts: 3);

        ProjectionDeliveryReconciliationResult result = await reconciler.ReconcileFromEventStoreAsync(
            Identity,
            "order-detail",
            "operator-7");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.StateUnavailable);
        result.PreservedSequence.ShouldBe(2);
        _ = await store.Received(3).TrySaveWithReconciliationAsync(
            Identity,
            "order-detail",
            Arg.Any<ProjectionDeliveryState>(),
            Arg.Any<ProjectionDeliveryReconciliationWork>(),
            "etag",
            Arg.Any<CancellationToken>());
    }

    private static ProjectionDeliveryReconciler CreateReconciler(
        IProjectionDeliveryStateStore store,
        IProjectionDeliveryHistoryReader history,
        int maxStateTransitionAttempts = 8) => new(
            store,
            history,
            Options.Create(new ProjectionDeliveryIdempotencyOptions {
                MaxStateTransitionAttempts = maxStateTransitionAttempts,
            }),
            new FakeTimeProvider(Now));

    private static IProjectionDeliveryStateStore StoreReturning(
        ProjectionDeliveryState state,
        ProjectionDeliveryStateClassification classification) {
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, "order-detail", Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(state, "etag", classification, false));
        return store;
    }

    private static ProjectionDeliveryState LegacyState(long sequence) => new(
        0,
        0,
        Identity.TenantId,
        Identity.Domain,
        Identity.AggregateId,
        null,
        sequence,
        null,
        null,
        null,
        0,
        null,
        ProjectionDeliveryMigrationProvenance.None,
        Now);

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
