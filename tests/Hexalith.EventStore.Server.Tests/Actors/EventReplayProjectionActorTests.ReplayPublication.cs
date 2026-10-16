using System.Security.Cryptography;
using System.Runtime.Serialization;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public partial class EventReplayProjectionActorTests
{
    [Theory]
    [InlineData("stage", false)]
    [InlineData("stage", true)]
    [InlineData("promote", false)]
    [InlineData("promote", true)]
    [InlineData("discard", false)]
    [InlineData("discard", true)]
    [InlineData("rollback", false)]
    [InlineData("rollback", true)]
    [InlineData("finalize", false)]
    [InlineData("finalize", true)]
    public async Task OrdinaryRebuild_AmbiguousSaveCannotResurrectDirtyModelOrOrderingAfterBoundPublication(string boundary, bool committed)
    {
        var (actor, state, lifecycle, _, _) = CreatePublicationActor();
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest prior = PublicationRequest(await lifecycle.ReadSnapshotAsync().ConfigureAwait(true));
        await actor.StageReplayAsync(prior).ConfigureAwait(true);
        await actor.PromoteReplayAsync(prior).ConfigureAwait(true);
        await actor.FinalizeReplayAsync(prior).ConfigureAwait(true);
        var candidate = new ProjectionRebuildCandidate("operator", prior.State! with { StateBytes = "{\"count\":2}"u8.ToArray() })
        { SelectedReplayFence = new("tenant-a:counter:counter-1", TestProjectionType, "operator", 2) };
        if (boundary != "stage") await actor.StageProjectionAsync(candidate).ConfigureAwait(true);
        if (boundary is "rollback" or "finalize") await actor.PromoteProjectionAsync(new("operator")).ConfigureAwait(true);
        int save = state.Trace.Count(item => item == "SaveState") + 1;
        if (committed) state.FaultAfterCall("SaveState", save, new TimeoutException("lost committed response"));
        else state.FaultOnCall("SaveState", save, new TimeoutException("before commit"));
        async Task ActAsync()
        {
            if (boundary == "stage") await actor.StageProjectionAsync(candidate).ConfigureAwait(true);
            else if (boundary == "promote") _ = await actor.PromoteProjectionAsync(new("operator")).ConfigureAwait(true);
            else if (boundary == "discard") _ = await actor.DiscardProjectionAsync(new("operator")).ConfigureAwait(true);
            else if (boundary == "rollback") _ = await actor.RollbackProjectionAsync(new("operator")).ConfigureAwait(true);
            else _ = await actor.FinalizeProjectionAsync(new("operator")).ConfigureAwait(true);
        }
        await Should.ThrowAsync<TimeoutException>(ActAsync).ConfigureAwait(true);
        string selected = System.Text.Json.JsonSerializer.Serialize(state.CreateCommittedView());
        await state.SaveStateAsync().ConfigureAwait(true);
        System.Text.Json.JsonSerializer.Serialize(state.CreateCommittedView()).ShouldBe(selected);
        ProjectionState actual = (await actor.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldNotBeNull();
        bool newer = boundary == "promote" ? committed : boundary == "rollback" ? !committed : boundary == "finalize";
        actual.StateBytes.ShouldBe(newer ? candidate.State.StateBytes : prior.State!.StateBytes);
    }

    [Fact]
    public void BoundReplay_ActorRemotingContractsRoundTripEveryNestedPublicationField()
    {
        ProjectionReplayPublicationRequest request = PublicationRequest(new(ProjectionLifecyclePhase.Delivering, 42));
        var serializer = new DataContractSerializer(typeof(ProjectionReplayPublicationRequest));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, request);
        stream.Position = 0;
        var read = (ProjectionReplayPublicationRequest)serializer.ReadObject(stream)!;
        read.Binding.ShouldBe(request.Binding);
        read.Lifecycle.ShouldBe(request.Lifecycle);
        read.LifecycleOperationId.ShouldBe(request.LifecycleOperationId);
        read.State!.StateBytes.ShouldBe(request.State!.StateBytes);
        read.State.TenantId.ShouldBe(request.State.TenantId);
        var result = new ProjectionRebuildInspection(request.Binding.OperationId, ProjectionRebuildInspectionStatus.Staged, request.State);
        serializer = new(typeof(ProjectionRebuildInspection));
        stream.SetLength(0);
        serializer.WriteObject(stream, result);
        stream.Position = 0;
        var returned = (ProjectionRebuildInspection)serializer.ReadObject(stream)!;
        returned.OperationId.ShouldBe(result.OperationId);
        returned.Status.ShouldBe(result.Status);
        returned.State!.StateBytes.ShouldBe(result.State!.StateBytes);
        var candidate = new ProjectionRebuildCandidate(request.Binding.OperationId, request.State)
        { PublicationBinding = request.Binding, PublicationFloor = request.Binding,
            SelectedReplayFence = new("tenant-a:counter:counter-1", TestProjectionType, "operation-2", 2),
            PriorReplayFence = new("tenant-a:counter:counter-1", TestProjectionType, "operation-1", 1) };
        serializer = new(typeof(ProjectionRebuildCandidate)); stream.SetLength(0);
        serializer.WriteObject(stream, candidate); stream.Position = 0;
        var replayCandidate = (ProjectionRebuildCandidate)serializer.ReadObject(stream)!;
        replayCandidate.PublicationBinding.ShouldBe(candidate.PublicationBinding);
        replayCandidate.PublicationFloor.ShouldBe(candidate.PublicationFloor);
        replayCandidate.SelectedReplayFence.ShouldBe(candidate.SelectedReplayFence);
        replayCandidate.PriorReplayFence.ShouldBe(candidate.PriorReplayFence);
        var lease = new ProjectionDeliveryLeaseProof(request.LifecycleOperationId, request.Lifecycle);
        serializer = new(typeof(ProjectionDeliveryLeaseProof)); stream.SetLength(0);
        serializer.WriteObject(stream, lease); stream.Position = 0;
        var replayLease = (ProjectionDeliveryLeaseProof)serializer.ReadObject(stream)!;
        replayLease.ShouldBe(lease);
    }

    [Fact]
    public async Task BoundReplay_InitialFrozenWorkCannotRegressOperatorRebuildWithNoPriorDeliveryFloor()
    {
        var (actor, state, lifecycle, notifier, _) = CreatePublicationActor();
        ProjectionReplayPublicationRequest frozen = PublicationRequest(new(ProjectionLifecyclePhase.Delivering, 1));
        await lifecycle.BeginRebuildAsync(new("operator-at-two")).ConfigureAwait(true);
        ProjectionState selected = frozen.State! with { StateBytes = "{\"count\":2}"u8.ToArray() };
        await actor.StageProjectionAsync(new("operator-at-two", selected)
        { SelectedReplayFence = new("tenant-a:counter:counter-1", TestProjectionType, "operator-at-two", 2) }).ConfigureAwait(true);
        await actor.PromoteProjectionAsync(new("operator-at-two")).ConfigureAwait(true);
        await actor.FinalizeProjectionAsync(new("operator-at-two")).ConfigureAwait(true);
        await lifecycle.CompleteRebuildAsync(new("operator-at-two")).ConfigureAwait(true);
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        int saves = state.Trace.Count(item => item == "SaveState");
        int notifications = notifier.ReceivedCalls().Count();
        frozen = frozen with { Lifecycle = await lifecycle.ReadSnapshotAsync().ConfigureAwait(true) };
        (await actor.StageReplayAsync(frozen).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Conflict);
        (await actor.ReadProjectionStateAsync().ConfigureAwait(true))!.StateBytes.ShouldBe(selected.StateBytes);
        state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
        notifier.ReceivedCalls().Count().ShouldBe(notifications);
    }

    [Theory]
    [InlineData("stage", false)]
    [InlineData("stage", true)]
    [InlineData("promote", false)]
    [InlineData("promote", true)]
    [InlineData("finalize", false)]
    [InlineData("finalize", true)]
    public async Task BoundReplay_AmbiguousSaveUsesPersistedReadbackAndDiscardsPendingCache(string boundary, bool committed)
    {
        var (actor, state, lifecycle, notifier, _) = CreatePublicationActor();
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest request = PublicationRequest(await lifecycle.ReadSnapshotAsync().ConfigureAwait(true));
        if (boundary != "stage") await actor.StageReplayAsync(request).ConfigureAwait(true);
        if (boundary == "finalize") await actor.PromoteReplayAsync(request).ConfigureAwait(true);
        int save = state.Trace.Count(item => item == "SaveState") + 1;
        if (committed) state.FaultAfterCall("SaveState", save, new TimeoutException("lost committed response"));
        else state.FaultOnCall("SaveState", save, new TimeoutException("before commit"));
        async Task ActAsync()
        {
            if (boundary == "stage") _ = await actor.StageReplayAsync(request).ConfigureAwait(true);
            else if (boundary == "promote") _ = await actor.PromoteReplayAsync(request).ConfigureAwait(true);
            else _ = await actor.FinalizeReplayAsync(request).ConfigureAwait(true);
        }
        if (committed) await ActAsync().ConfigureAwait(true);
        else await Should.ThrowAsync<TimeoutException>(ActAsync).ConfigureAwait(true);
        await state.SaveStateAsync().ConfigureAwait(true);
        ProjectionRebuildInspection persisted = await actor.InspectProjectionRebuildAsync(new("original")).ConfigureAwait(true);
        persisted.Status.ShouldBe(boundary == "stage"
            ? committed ? ProjectionRebuildInspectionStatus.Staged : ProjectionRebuildInspectionStatus.Missing
            : boundary == "promote" && !committed ? ProjectionRebuildInspectionStatus.Staged : ProjectionRebuildInspectionStatus.Promoted);
        if (boundary == "promote" || boundary == "stage") notifier.ReceivedCalls().ShouldBeEmpty();
        if (boundary == "finalize")
        {
            var receipt = (ProjectionRebuildPromotionReceipt)state.CommittedState[EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey];
            receipt.Completed.ShouldBe(committed);
            if (committed) { receipt.PreviousState.ShouldBeNull(); receipt.PromotedState.ShouldBeNull(); }
        }
    }

    [Fact]
    public async Task BoundReplay_CompletedTombstoneAndLeaseOwnershipPreventRestagingOrDuplicateNotification()
    {
        var (actor, state, lifecycle, notifier, _) = CreatePublicationActor();
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest request = PublicationRequest(await lifecycle.ReadSnapshotAsync().ConfigureAwait(true));
        (await actor.StageReplayAsync(request).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Staged);
        (await actor.PromoteReplayAsync(request).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        (await actor.FinalizeReplayAsync(request).ConfigureAwait(true)).ShouldBeTrue();
        int saves = state.Trace.Count(item => item == "SaveState");
        (await actor.StageReplayAsync(request).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        (await actor.PromoteReplayAsync(request).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
        await notifier.Received(1).NotifyProjectionChangedAsync(TestProjectionType, TestTenantId,
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
        var receipt = (ProjectionRebuildPromotionReceipt)state.CommittedState[EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey];
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(receipt).Length.ShouldBeLessThan(4096);
        await lifecycle.CompleteDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        await lifecycle.BeginDeliveryWriteAsync(new("competitor")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest stolen = request with { Lifecycle = await lifecycle.ReadSnapshotAsync().ConfigureAwait(true) };
        (await actor.StageReplayAsync(stolen).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Conflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundReplay_PausedOldWorkCannotRestageAfterNewerCompletionOrOrdinaryRebuildEvenWithFreshLease(bool rebuild)
    {
        var (actor, state, lifecycle, notifier, _) = CreatePublicationActor();
        await lifecycle.BeginDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest old = PublicationRequest(await lifecycle.ReadSnapshotAsync().ConfigureAwait(true));
        await actor.StageReplayAsync(old).ConfigureAwait(true);
        await actor.PromoteReplayAsync(old).ConfigureAwait(true);
        await actor.FinalizeReplayAsync(old).ConfigureAwait(true);
        await lifecycle.CompleteDeliveryWriteAsync(new("lease-1")).ConfigureAwait(true);
        if (rebuild)
        {
            await lifecycle.BeginRebuildAsync(new("operator-rebuild")).ConfigureAwait(true);
            await actor.StageProjectionAsync(new("operator-rebuild", old.State! with { StateBytes = "{\"count\":100}"u8.ToArray() })).ConfigureAwait(true);
            (await actor.PromoteProjectionAsync(new("operator-rebuild")).ConfigureAwait(true)).ShouldBeTrue();
            await actor.FinalizeProjectionAsync(new("operator-rebuild")).ConfigureAwait(true);
            await lifecycle.CompleteRebuildAsync(new("operator-rebuild")).ConfigureAwait(true);
        }
        else
        {
            await lifecycle.BeginDeliveryWriteAsync(new("newer")).ConfigureAwait(true);
            ProjectionReplayPublicationRequest newer = old with { Binding = old.Binding with { OperationId = "newer", HeadSequence = 2 },
                Lifecycle = await lifecycle.ReadSnapshotAsync().ConfigureAwait(true), LifecycleOperationId = "newer" };
            await actor.StageReplayAsync(newer).ConfigureAwait(true);
            await actor.PromoteReplayAsync(newer).ConfigureAwait(true);
            await actor.FinalizeReplayAsync(newer).ConfigureAwait(true);
            await lifecycle.CompleteDeliveryWriteAsync(new("newer")).ConfigureAwait(true);
        }
        await lifecycle.BeginDeliveryWriteAsync(new("stale-reacquirer")).ConfigureAwait(true);
        ProjectionReplayPublicationRequest stale = old with { Lifecycle = await lifecycle.ReadSnapshotAsync().ConfigureAwait(true),
            LifecycleOperationId = "stale-reacquirer" };
        int saves = state.Trace.Count(item => item == "SaveState");
        int notifications = notifier.ReceivedCalls().Count();
        (await actor.StageReplayAsync(stale).ConfigureAwait(true)).Status.ShouldBe(ProjectionRebuildInspectionStatus.Conflict);
        state.Trace.Count(item => item == "SaveState").ShouldBe(saves);
        notifier.ReceivedCalls().Count().ShouldBe(notifications);
        (await actor.ReadProjectionStateAsync().ConfigureAwait(true))!.StateBytes.ShouldBe(rebuild ? "{\"count\":100}"u8.ToArray() : old.State!.StateBytes);
    }

    private static (EventReplayProjectionActor Actor, FaultInjectingActorStateManager State, ProjectionLifecycleActor Lifecycle,
        IProjectionChangeNotifier Notifier, FakeTimeProvider Clock) CreatePublicationActor()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var lifecycle = new ProjectionLifecycleActor(ActorHost.CreateForTest<ProjectionLifecycleActor>(
            new ActorTestOptions { ActorId = new ActorId("tenant-a:counter:counter-1:counter-projection") }),
            NullLogger<ProjectionLifecycleActor>.Instance, clock);
        ActorStateManagerTestHelper.SetStateManager(lifecycle, new FaultInjectingActorStateManager());
        var gateway = Substitute.For<IProjectionLifecycleGateway>();
        gateway.OwnsDeliveryWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<ProjectionLifecycleSnapshot>(), Arg.Any<CancellationToken>()).Returns(call => lifecycle.OwnsDeliveryLeaseAsync(
                new(call.ArgAt<string>(2), call.ArgAt<ProjectionLifecycleSnapshot>(3))));
        IProjectionChangeNotifier notifier = Substitute.For<IProjectionChangeNotifier>();
        var actor = new EventReplayProjectionActor(ActorHost.CreateForTest<EventReplayProjectionActor>(
            new ActorTestOptions { ActorId = new ActorId("counter-projection:tenant-a:counter-1") }),
            Substitute.For<IETagService>(), notifier, NullLogger<EventReplayProjectionActor>.Instance);
        var state = new FaultInjectingActorStateManager();
        ActorStateManagerTestHelper.SetStateManager(actor, state);
        actor.SetPublicationLifecycle(gateway);
        return (actor, state, lifecycle, notifier, clock);
    }

    private static ProjectionReplayPublicationRequest PublicationRequest(ProjectionLifecycleSnapshot snapshot)
    {
        ProjectionState state = CreateTestState();
        return new(new(TestTenantId, "counter", "counter-1", TestProjectionType, "original", 1, "event-1",
            new string('a', 64), Convert.ToHexStringLower(SHA256.HashData(state.StateBytes))), snapshot, "lease-1", state);
    }
}
