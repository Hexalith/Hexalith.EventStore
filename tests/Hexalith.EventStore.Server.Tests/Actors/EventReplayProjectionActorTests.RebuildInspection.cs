using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public partial class EventReplayProjectionActorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectRebuild_ReadsOriginalCandidateOrPromotionWithoutEffects(bool promoted)
    {
        (EventReplayProjectionActor actor, IActorStateManager state, var notifier, _) = CreateActor();
        ProjectionState candidate = CreateTestState();
        state.TryGetStateAsync<ProjectionRebuildPromotionReceipt>(EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey,
            Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ProjectionRebuildPromotionReceipt>(promoted,
                new("original", null, candidate)));
        state.TryGetStateAsync<ProjectionRebuildCandidate>(EventReplayProjectionActor.ProjectionRebuildCandidateKey,
            Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ProjectionRebuildCandidate>(true, new("original", candidate)));
        state.TryGetStateAsync<ProjectionState>(EventReplayProjectionActor.ProjectionStateKey,
            Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ProjectionState>(true,
                candidate with { StateBytes = candidate.StateBytes.ToArray() }));

        ProjectionRebuildInspection result = await actor.InspectProjectionRebuildAsync(new("original")).ConfigureAwait(true);

        result.Status.ShouldBe(promoted ? ProjectionRebuildInspectionStatus.Promoted : ProjectionRebuildInspectionStatus.Staged);
        result.State.ShouldBe(candidate);
        state.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "TryGetStateAsync" || call.GetMethodInfo().Name == "ClearCacheAsync");
        notifier.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("changed-live")]
    public async Task InspectRebuild_RejectsForeignReceiptAndChangedLiveStateWithoutReturningModel(string invalid)
    {
        (EventReplayProjectionActor actor, IActorStateManager state, _, _) = CreateActor();
        ProjectionState candidate = CreateTestState();
        state.TryGetStateAsync<ProjectionRebuildPromotionReceipt>(EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey,
            Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ProjectionRebuildPromotionReceipt>(true,
                new(invalid == "foreign" ? "another" : "original", null, candidate)));
        state.TryGetStateAsync<ProjectionState>(EventReplayProjectionActor.ProjectionStateKey,
            Arg.Any<CancellationToken>()).Returns(new ConditionalValue<ProjectionState>(true,
                candidate with { StateBytes = "{}"u8.ToArray() }));

        ProjectionRebuildInspection result = await actor.InspectProjectionRebuildAsync(new("original")).ConfigureAwait(true);

        result.Status.ShouldBe(ProjectionRebuildInspectionStatus.Conflict);
        result.State.ShouldBeNull();
        state.ReceivedCalls().ShouldAllBe(call => call.GetMethodInfo().Name == "TryGetStateAsync" || call.GetMethodInfo().Name == "ClearCacheAsync");
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InspectRebuild_SameActorDiscardsUncommittedCacheAndReadsLostCommittedResponse(bool promote, bool committed)
    {
        (EventReplayProjectionActor actor, _, var notifier, _) = CreateActor();
        var state = new FaultInjectingActorStateManager();
        ProjectionState original = CreateTestState() with { StateBytes = "{\"count\":1}"u8.ToArray() };
        ProjectionState candidate = CreateTestState();
        var seed = new Dictionary<string, object> { [EventReplayProjectionActor.ProjectionStateKey] = original };
        if (promote) seed[EventReplayProjectionActor.ProjectionRebuildCandidateKey] = new ProjectionRebuildCandidate("original", candidate);
        await state.SeedCommittedStateAsync(seed).ConfigureAwait(true);
        if (committed) state.FaultAfterCall("SaveState", 1, new TimeoutException("lost committed response"));
        else state.FaultOnCall("SaveState", 1, new TimeoutException("failed before commit"));
        ActorStateManagerTestHelper.SetStateManager(actor, state);
        await Should.ThrowAsync<TimeoutException>(() => promote
            ? actor.PromoteProjectionAsync(new("original"))
            : actor.StageProjectionAsync(new("original", candidate))).ConfigureAwait(true);

        ProjectionRebuildInspection result = await actor.InspectProjectionRebuildAsync(new("original")).ConfigureAwait(true);

        result.Status.ShouldBe(promote
            ? committed ? ProjectionRebuildInspectionStatus.Promoted : ProjectionRebuildInspectionStatus.Staged
            : committed ? ProjectionRebuildInspectionStatus.Staged : ProjectionRebuildInspectionStatus.Missing);
        // A subsequent unrelated save must not publish the discarded cache from the failed attempt.
        await state.SaveStateAsync().ConfigureAwait(true);
        ProjectionState live = (ProjectionState)state.CommittedState[EventReplayProjectionActor.ProjectionStateKey];
        live.StateBytes.ShouldBe(promote && committed ? candidate.StateBytes : original.StateBytes);
        state.CommittedState.ContainsKey(EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey).ShouldBe(promote && committed);
        int failedSave = state.Trace.IndexOf("SaveState");
        state.Trace.FindIndex(failedSave + 1, static operation => operation == "ClearCache").ShouldBeGreaterThan(failedSave);
        notifier.ReceivedCalls().ShouldBeEmpty();
    }

}
