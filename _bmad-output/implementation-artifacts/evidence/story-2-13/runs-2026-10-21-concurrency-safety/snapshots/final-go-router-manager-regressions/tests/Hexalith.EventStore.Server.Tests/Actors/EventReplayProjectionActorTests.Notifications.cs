using Dapr.Actors.Runtime;
using System.Security.Cryptography;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public partial class EventReplayProjectionActorTests
{
    [Fact]
    public async Task UpdateProjectionAsync_OptionalDetail_IsBoundedAndPersistedBeforeSignalAndDetail()
    {
        var (actor, manager, notifier, _) = CreateActor();
        ProjectionState state = CreateTestState() with
        {
            NotificationGroupScope = "counter-status",
            NotificationMetadata = Enumerable.Range(0, 20).ToDictionary(index => $"k{index:D2}", _ => new string('x', 100)),
        };
        ProjectionState? persisted = null;
        _ = manager.SetStateAsync(EventReplayProjectionActor.ProjectionStateKey,
            Arg.Do<ProjectionState>(value => persisted = value), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await actor.UpdateProjectionAsync(state);
        persisted.ShouldNotBeNull().NotificationMetadata.ShouldNotBeNull().Count.ShouldBe(16);
        persisted.StateBytes.ShouldBe(state.StateBytes);
        Received.InOrder(() =>
        {
            _ = manager.SaveStateAsync(Arg.Any<CancellationToken>());
            _ = notifier.NotifyProjectionChangedAsync(TestProjectionType, TestTenantId, null, Arg.Any<CancellationToken>());
            _ = notifier.NotifyProjectionChangedAsync(Arg.Is<ProjectionChangedDetail>(detail =>
                detail.ProjectionType == TestProjectionType && detail.TenantId == TestTenantId
                && detail.GroupScope == "counter-status" && detail.Metadata.Count == 16), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StageProjectionAsync_OptionalDetail_IsBoundedWithoutPublishingBeforePromotion()
    {
        var (actor, manager, notifier, _) = CreateActor();
        ProjectionState state = CreateTestState() with
        {
            NotificationGroupScope = "counter-status",
            NotificationMetadata = Enumerable.Range(0, 20).ToDictionary(index => $"k{index:D2}", _ => new string('é', 100)),
        };
        _ = manager.TryGetStateAsync<ProjectionRebuildCandidate>(
            EventReplayProjectionActor.ProjectionRebuildCandidateKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<ProjectionRebuildCandidate>(false, default!));
        await actor.StageProjectionAsync(new ProjectionRebuildCandidate("detail-operation", state));
        await manager.Received(1).SetStateAsync(EventReplayProjectionActor.ProjectionRebuildCandidateKey,
            Arg.Is<ProjectionRebuildCandidate>(candidate => HasBoundedMetadata(candidate)),
            Arg.Any<CancellationToken>());
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default!, default!, default, default);
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default(ProjectionChangedDetail)!, default(CancellationToken));
    }

    [Fact]
    public async Task UpdateProjectionAsync_InvalidOptionalScope_PerformsNoPersistenceOrPublication()
    {
        var (actor, manager, notifier, _) = CreateActor();
        ProjectionState state = CreateTestState() with { NotificationGroupScope = "forged:group" };
        await Should.ThrowAsync<ArgumentException>(() => actor.UpdateProjectionAsync(state));
        await manager.DidNotReceiveWithAnyArgs().SaveStateAsync(default);
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default!, default!, default, default);
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default(ProjectionChangedDetail)!, default(CancellationToken));
    }

    [Fact]
    public async Task BoundReplay_OptionalDetail_IsCommittedBeforePublicationAndCompletedReplayDoesNotRepublish()
    {
        var (actor, state, lifecycle, notifier, _) = CreatePublicationActor();
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1"));
        ProjectionReplayPublicationRequest request = PublicationRequest(await lifecycle.ReadSnapshotAsync());
        request = request with
        {
            State = request.State! with
            {
                NotificationGroupScope = "counter-status",
                NotificationMetadata = Enumerable.Range(0, 20).ToDictionary(index => $"k{index:D2}", _ => new string('é', 100)),
            },
        };
        string modelFingerprint = Convert.ToHexStringLower(SHA256.HashData(request.State!.StateBytes));
        modelFingerprint.ShouldBe(request.Binding.CandidateFingerprint);
        notifier.When(value => value.NotifyProjectionChangedAsync(Arg.Any<ProjectionChangedDetail>(), Arg.Any<CancellationToken>()))
            .Do(_ =>
            {
                var committed = (ProjectionState)state.CommittedState[EventReplayProjectionActor.ProjectionStateKey];
                committed.NotificationGroupScope.ShouldBe("counter-status");
                committed.NotificationMetadata.ShouldNotBeNull();
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(committed.NotificationMetadata).Length.ShouldBeLessThanOrEqualTo(2_048);
                Convert.ToHexStringLower(SHA256.HashData(committed.StateBytes)).ShouldBe(modelFingerprint);
            });

        (await actor.StageReplayAsync(request)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Staged);
        notifier.ReceivedCalls().ShouldBeEmpty();
        var staged = (ProjectionRebuildCandidate)state.CommittedState[EventReplayProjectionActor.ProjectionRebuildCandidateKey];
        staged.State.NotificationMetadata.ShouldNotBeNull().Count.ShouldBeLessThan(request.State.NotificationMetadata!.Count);
        (await actor.PromoteReplayAsync(request)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        var live = (await actor.ReadProjectionStateAsync()).ShouldNotBeNull();
        live.NotificationGroupScope.ShouldBe(staged.State.NotificationGroupScope);
        live.NotificationMetadata.ShouldBe(staged.State.NotificationMetadata);
        (await actor.FinalizeReplayAsync(request)).ShouldBeTrue();
        int saves = state.Trace.Count(item => item == "SaveState");
        (await actor.StageReplayAsync(request)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        (await actor.PromoteReplayAsync(request)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
        await notifier.Received(1).NotifyProjectionChangedAsync(TestProjectionType, TestTenantId, null, Arg.Any<CancellationToken>());
        await notifier.Received(1).NotifyProjectionChangedAsync(Arg.Is<ProjectionChangedDetail>(detail =>
            detail.ProjectionType == TestProjectionType && detail.TenantId == TestTenantId && detail.GroupScope == "counter-status"),
            Arg.Any<CancellationToken>());
        var receipt = (ProjectionRebuildPromotionReceipt)state.CommittedState[EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey];
        receipt.SelectedStateFingerprint.ShouldBe(modelFingerprint);
    }

    private static bool HasBoundedMetadata(ProjectionRebuildCandidate candidate)
        => candidate.State.NotificationMetadata is not null && candidate.State.NotificationMetadata.Count <= 16
            && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(candidate.State.NotificationMetadata).Length <= 2_048;
}
