using Dapr.Actors.Runtime;

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
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default!, default);
    }

    [Fact]
    public async Task UpdateProjectionAsync_InvalidOptionalScope_PerformsNoPersistenceOrPublication()
    {
        var (actor, manager, notifier, _) = CreateActor();
        ProjectionState state = CreateTestState() with { NotificationGroupScope = "forged:group" };
        await Should.ThrowAsync<ArgumentException>(() => actor.UpdateProjectionAsync(state));
        await manager.DidNotReceiveWithAnyArgs().SaveStateAsync(default);
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default!, default!, default, default);
        await notifier.DidNotReceiveWithAnyArgs().NotifyProjectionChangedAsync(default!, default);
    }

    private static bool HasBoundedMetadata(ProjectionRebuildCandidate candidate)
        => candidate.State.NotificationMetadata is not null && candidate.State.NotificationMetadata.Count <= 16
            && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(candidate.State.NotificationMetadata).Length <= 2_048;
}
