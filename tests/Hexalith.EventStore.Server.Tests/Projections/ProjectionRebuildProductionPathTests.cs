using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionRebuildProductionPathTests {
    [Fact]
    public async Task Rebuild_MoreThanTwoPages_PersistsOracleEquivalentActorDetailIndexVersionsAndCheckpoints() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7);

        await harness.RunAsync();

        await AssertConvergedAsync(harness);
        harness.ActorSnapshot().EventCount.ShouldBe(7);
        _ = await harness.DeliveryCheckpoints.DidNotReceiveWithAnyArgs()
            .SaveDeliveredSequenceAsync(default!, default!, default, default);
        _ = await harness.CheckpointTracker.DidNotReceiveWithAnyArgs()
            .SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Theory]
    [InlineData(3, null)]
    [InlineData(7, 5)]
    public async Task Rebuild_ExactBoundaryAndBoundedTarget_PersistOracleEquivalentState(
        int eventCount,
        int? toPosition) {
        using var harness = new ProjectionRebuildProductionHarness(eventCount, toPosition);

        await harness.RunAsync();

        await AssertConvergedAsync(harness);
    }

    [Fact]
    public async Task Rebuild_EmptyStream_PreservesExistingLiveStateAndCompletesWithoutProjectionWrite() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 0);

        await harness.RunAsync();

        harness.ActorSnapshot().ShouldBe(harness.OldDetail);
        harness.ReadModels.Snapshot<AggregateReadModel>(
            ProjectionRebuildProductionHarness.StoreName,
            ProjectionRebuildProductionHarness.DetailKey).ShouldBe(harness.OldDetail);
        AggregateIndexReadModel index = harness.ReadModels.Snapshot<AggregateIndexReadModel>(
            ProjectionRebuildProductionHarness.StoreName,
            ProjectionRebuildProductionHarness.IndexKey).ShouldNotBeNull();
        index.AggregateIds.ShouldBe(harness.OldIndex.AggregateIds);
        index.ProjectionVersion.ShouldBe(harness.OldIndex.ProjectionVersion);
        ProjectionRebuildCheckpoint frozen = harness.RebuildCheckpoints
            .Snapshot(harness.AggregateScope).ShouldNotBeNull();
        frozen.LastAppliedSequence.ShouldBe(0);
        frozen.ToPosition.ShouldBe(0);
        harness.RebuildCheckpoints.Snapshot(harness.OperatorScope)!.Status
            .ShouldBe(ProjectionRebuildStatus.Succeeded);
        await harness.ProjectionWriteActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
    }

    [Fact]
    public async Task Rebuild_CanceledAfterFirstPage_PreservesEveryLiveSurfaceAndClearsLifecycle() {
        using var source = new CancellationTokenSource();
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7);
        harness.PageReadOverride = (cursor, bound, maxCount) => {
            if (cursor == 0) {
                return harness.Events
                    .Where(item => bound is null || item.SequenceNumber <= bound.Value)
                    .Take(maxCount)
                    .ToArray();
            }

            source.Cancel();
            throw new OperationCanceledException(source.Token);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(() => harness.RunAsync(source.Token));

        await AssertOldLiveViewAsync(harness);
        ProjectionRebuildCheckpoint checkpoint = harness.RebuildCheckpoints
            .Snapshot(harness.OperatorScope).ShouldNotBeNull();
        checkpoint.Status.ShouldBe(ProjectionRebuildStatus.Canceled);
        checkpoint.FailureReasonCode.ShouldBe(StreamReplayReasonCodes.RebuildCanceled);
        _ = await harness.Lifecycle.Received(1).CompleteRebuildAsync(
            harness.Identity,
            harness.OperatorScope.ProjectionName,
            ProjectionRebuildProductionHarness.OperationId,
            CancellationToken.None);
    }

    [Fact]
    public async Task Rebuild_InterruptedStorePromotion_PreservesLiveViewAndSameOperationRetryConverges() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7);
        harness.ReadModels.BatchFaultHook = (phase, _, _) =>
            phase == ReadModelBatchPhase.BeforeCommit
                ? throw new TimeoutException("injected before commit")
                : Task.CompletedTask;

        await harness.RunAsync();

        await AssertOldLiveViewAsync(harness, resolveBatchVisibility: true);
        ProjectionRebuildCheckpoint interrupted = harness.RebuildCheckpoints
            .Snapshot(harness.OperatorScope).ShouldNotBeNull();
        interrupted.Status.ShouldBe(ProjectionRebuildStatus.Running);
        interrupted.FailureReasonCode.ShouldBeNull();
        harness.ReadModels.BatchFaultHook = null;

        await harness.RunAsync();

        await AssertConvergedAsync(harness);
    }

    [Fact]
    public async Task Rebuild_PromotionFenceReleaseFailure_RemainsRunningAndRetryCompletesCleanup() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7);
        bool permitLifecycleCompletion = false;
        _ = harness.Lifecycle.CompleteRebuildPromotionAsync(
                harness.Identity,
                Arg.Any<string>(),
                ProjectionRebuildProductionHarness.OperationId,
                Arg.Any<CancellationToken>())
            .Returns(_ => permitLifecycleCompletion);
        _ = harness.Lifecycle.CompleteRebuildAsync(
                harness.Identity,
                Arg.Any<string>(),
                ProjectionRebuildProductionHarness.OperationId,
                Arg.Any<CancellationToken>())
            .Returns(_ => permitLifecycleCompletion);
        _ = harness.Lifecycle.ReadPhaseAsync(
                harness.Identity,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => permitLifecycleCompletion
                ? ProjectionLifecyclePhase.Idle
                : ProjectionLifecyclePhase.Rebuilding);

        await harness.RunAsync();

        harness.RebuildCheckpoints.Snapshot(harness.OperatorScope)!.Status
            .ShouldBe(ProjectionRebuildStatus.Running);
        harness.ActorSnapshot().ShouldBe(harness.Expected!.Detail);

        permitLifecycleCompletion = true;
        await harness.RunAsync();

        await AssertConvergedAsync(harness);
    }

    [Fact]
    public async Task Rebuild_HandlerPreparationFailure_PreservesEveryLiveSurfaceAndRemainsResumable() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7) {
            FailIndexPreparation = true,
        };

        await harness.RunAsync();

        await AssertOldLiveViewAsync(harness);
        ProjectionRebuildCheckpoint interrupted = harness.RebuildCheckpoints
            .Snapshot(harness.OperatorScope).ShouldNotBeNull();
        interrupted.Status.ShouldBe(ProjectionRebuildStatus.Running);
        interrupted.FailureReasonCode.ShouldBeNull();
        _ = await harness.Lifecycle.DidNotReceiveWithAnyArgs().CompleteRebuildAsync(default!, default!, default!, default);

        harness.FailIndexPreparation = false;
        await harness.RunAsync();
        await AssertConvergedAsync(harness);
    }

    [Fact]
    public async Task Rebuild_TerminalNamedFailureDiscardsCandidatePreservesReasonAndReleasesEveryLifecycle() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7) {
            FailIndexTerminal = true,
        };

        await harness.RunAsync();

        await AssertOldLiveViewAsync(harness);
        ProjectionRebuildCheckpoint failed = harness.RebuildCheckpoints
            .Snapshot(harness.OperatorScope).ShouldNotBeNull();
        failed.Status.ShouldBe(ProjectionRebuildStatus.Failed);
        failed.FailureReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        _ = await harness.RebuildWrites.Received(1).DiscardAsync(
            Arg.Any<string>(),
            ProjectionRebuildProductionHarness.OperationId,
            CancellationToken.None);
        _ = await harness.Lifecycle.Received().CompleteRebuildAsync(
            harness.Identity,
            "aggregate-detail",
            ProjectionRebuildProductionHarness.OperationId,
            CancellationToken.None);
        _ = await harness.Lifecycle.Received().CompleteRebuildAsync(
            harness.Identity,
            "aggregate-index",
            ProjectionRebuildProductionHarness.OperationId,
            CancellationToken.None);
    }

    [Fact]
    public async Task Rebuild_ResumeFromPersistedProgress_ReplaysCompletePrefixAndConverges() {
        using var harness = new ProjectionRebuildProductionHarness(eventCount: 7);
        harness.SeedAggregateProgress(3);

        await harness.RunAsync();

        await AssertConvergedAsync(harness);
        long[] cursors = [.. harness.AggregateActor.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(harness.AggregateActor.ReadEventsRangeAsync))
            .Select(call => (long)call.GetArguments()[0]!)];
        cursors.ShouldBe([0, 3, 6]);
    }

    private static async Task AssertConvergedAsync(ProjectionRebuildProductionHarness harness) {
        ProjectionRebuildEquivalenceSnapshot expected = harness.Expected.ShouldNotBeNull();
        AggregateReadModel actor = harness.ActorSnapshot();
        actor.ShouldBe(expected.Detail);

        AggregateReadModel detail = harness.ReadModels.Snapshot<AggregateReadModel>(
            ProjectionRebuildProductionHarness.StoreName,
            ProjectionRebuildProductionHarness.DetailKey).ShouldNotBeNull();
        detail.ShouldBe(expected.Detail);
        AggregateIndexReadModel index = harness.ReadModels.Snapshot<AggregateIndexReadModel>(
            ProjectionRebuildProductionHarness.StoreName,
            ProjectionRebuildProductionHarness.IndexKey).ShouldNotBeNull();
        index.AggregateIds.ShouldBe(expected.Index.AggregateIds);
        index.ProjectedAt.ShouldBe(expected.Index.ProjectedAt);
        index.ProjectionVersion.ShouldBe(expected.Index.ProjectionVersion);
        ((IReadModelFreshness)detail).ProjectionVersion.ShouldBe(expected.ProjectionVersion);
        ((IReadModelFreshness)index).ProjectionVersion.ShouldBe(expected.ProjectionVersion);
        detail.ProjectionVersion.ShouldNotBe("transport-etag-not-a-projection-version");

        ProjectionRebuildCheckpoint aggregateCheckpoint = harness.RebuildCheckpoints
            .Snapshot(harness.AggregateScope).ShouldNotBeNull();
        aggregateCheckpoint.LastAppliedSequence.ShouldBe(expected.Checkpoint);
        aggregateCheckpoint.Status.ShouldBe(ProjectionRebuildStatus.Running);
        ProjectionRebuildCheckpoint operatorCheckpoint = harness.RebuildCheckpoints
            .Snapshot(harness.OperatorScope).ShouldNotBeNull();
        operatorCheckpoint.Status.ShouldBe(ProjectionRebuildStatus.Succeeded);
        operatorCheckpoint.OperationId.ShouldBe(ProjectionRebuildProductionHarness.OperationId);
        _ = await harness.Lifecycle.Received().CompleteRebuildAsync(
            harness.Identity,
            harness.OperatorScope.ProjectionName,
            ProjectionRebuildProductionHarness.OperationId,
            CancellationToken.None);
    }

    private static async Task AssertOldLiveViewAsync(
        ProjectionRebuildProductionHarness harness,
        bool resolveBatchVisibility = false) {
        harness.ActorSnapshot().ShouldBe(harness.OldDetail);
        AggregateReadModel detail = resolveBatchVisibility
            ? (await harness.ReadModels.GetAsync<AggregateReadModel>(
                ProjectionRebuildProductionHarness.StoreName,
                ProjectionRebuildProductionHarness.DetailKey)).Value.ShouldNotBeNull()
            : harness.ReadModels.Snapshot<AggregateReadModel>(
                ProjectionRebuildProductionHarness.StoreName,
                ProjectionRebuildProductionHarness.DetailKey).ShouldNotBeNull();
        AggregateIndexReadModel index = resolveBatchVisibility
            ? (await harness.ReadModels.GetAsync<AggregateIndexReadModel>(
                ProjectionRebuildProductionHarness.StoreName,
                ProjectionRebuildProductionHarness.IndexKey)).Value.ShouldNotBeNull()
            : harness.ReadModels.Snapshot<AggregateIndexReadModel>(
                ProjectionRebuildProductionHarness.StoreName,
                ProjectionRebuildProductionHarness.IndexKey).ShouldNotBeNull();
        detail.ShouldBe(harness.OldDetail);
        index.AggregateIds.ShouldBe(harness.OldIndex.AggregateIds);
        index.ProjectedAt.ShouldBe(harness.OldIndex.ProjectedAt);
        index.ProjectionVersion.ShouldBe(harness.OldIndex.ProjectionVersion);
        await harness.ProjectionWriteActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
    }
}
