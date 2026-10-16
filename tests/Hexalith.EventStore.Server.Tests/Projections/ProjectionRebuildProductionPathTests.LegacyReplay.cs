using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Events;
using System.Text.Json;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionRebuildProductionPathTests
{
    [Fact]
    public async Task LegacyFallback_ReleasedCompletedRowWithExistingActorSettlesReadOnlyThenNextHeadUsesBoundedReplay()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2).ConfigureAwait(true);
        await fixture.SeedReleasedCompletedRowAndActorAsync().ConfigureAwait(true);
        ProjectionDeliveryStateReadResult before = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionDeliveryStateReadResult duplicate = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        duplicate.Etag.ShouldBe(before.Etag);
        JsonSerializer.Serialize(duplicate.State).ShouldBe(JsonSerializer.Serialize(before.State));
        fixture.ModelState.CommittedState.Keys.ShouldBe([EventReplayProjectionActor.ProjectionStateKey]);
        fixture.Notifications.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.AppendAsync(1).ConfigureAwait(true);
        await fixture.Outbox.EnsureAsync(fixture.Identity).ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(3);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(3);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task LegacyFallback_UnactivatedWriterRetainsCompatibleScopedCheckpointProtocol()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257, writerActive: false).ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).ReadDeliveredSequenceAsync(fixture.Identity, "counter")
            .ConfigureAwait(true)).ShouldBe(257);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(257);
        (await fixture.Rows.ReadWriterProtocolAsync().ConfigureAwait(true)).ShouldBeNull();
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task LegacyFallback_NoSourceAnchorUsesCanonicalReplayWithoutManufacturingGuardEvidence()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
            new AggregateMetadata(257, fixture.Clock.GetUtcNow(), null)).ConfigureAwait(true);
        await fixture.SourceState.SaveStateAsync().ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionDeliveryStateReadResult read = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        read.State!.LastDeliveredSequence.ShouldBe(257);
        read.State.SchemaVersion.ShouldBe(ProjectionDeliveryState.CurrentSchemaVersion);
        read.State.GuardEvidence.ShouldBeNull(); read.SourceGuard.ShouldBeNull();
        (await fixture.Source.InspectSourceProofAsync().ConfigureAwait(true)).Status.ShouldBe(SourceProofInspectionStatus.Unsupported);
    }

    [Fact]
    public async Task LegacyFallback_GuardedScopeRemainsFencedWhenCatalogDescriptorIsAbsent()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2).ConfigureAwait(true);
        ProjectionDeliveryState guarded = ProjectionDeliveryState.CreateEmpty(fixture.Identity, "counter",
            ProjectionDeliveryFingerprint.ComputeInitial(fixture.Identity, "counter"), fixture.Clock.GetUtcNow()) with
        { SchemaVersion = ProjectionDeliveryState.GuardSchemaVersion, GuardEvidenceVersion = 1 };
        (await fixture.Rows.TrySaveAsync(fixture.Identity, "counter", guarded, string.Empty).ConfigureAwait(true)).ShouldBeTrue();
        ProjectionDeliveryStateReadResult before = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldBeNull();
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).Etag.ShouldBe(before.Etag);
        fixture.Notifications.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task LegacyFallback_OverOwnerLimitFailsBeforeHistoryPreparationOrPublication()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(10_001).ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.DeliverAsync().ConfigureAwait(true);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(0);
        fixture.Http.Preparations.ShouldBeEmpty();
        fixture.ModelState.CommittedState.ShouldBeEmpty();
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State.ShouldBeNull();
    }

    [Fact]
    public async Task LegacyFallback_ChangedStoredPrefixAfterStagePreservesCandidatePendingWorkAndOldCompletion()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        fixture.AfterStage = async () =>
        {
            await fixture.SourceState.SetStateAsync(fixture.Identity.EventStreamKeyPrefix + 2,
                fixture.Events[1] with { UserId = "changed-user" }).ConfigureAwait(false);
            await fixture.SourceState.SaveStateAsync().ConfigureAwait(false);
        };
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldBeNull();
        ProjectionDeliveryState row = (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State.ShouldNotBeNull();
        row.LastDeliveredSequence.ShouldBe(0); row.ActiveReservation.ShouldNotBeNull();
        fixture.Notifications.ReceivedCalls().ShouldBeEmpty();
        ProjectionLegacyDeliveryIntent intent = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery.ShouldNotBeNull();
        (await fixture.Model.InspectProjectionRebuildAsync(new(intent.OperationId)).ConfigureAwait(true)).Status
            .ShouldBe(ProjectionRebuildInspectionStatus.Staged);
    }

    [Fact]
    public async Task LegacyFallback_PausedBeforeLeaseCannotRegressNewOperatorSelectionAndRebuildDoesNotAdvanceDeliveryCheckpoint()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1).ConfigureAwait(true);
        fixture.BeforeLease = async () =>
        {
            await fixture.AppendAsync(1).ConfigureAwait(false);
            await fixture.Outbox.EnsureAsync(fixture.Identity).ConfigureAwait(false);
            await fixture.LifecycleActor.BeginRebuildAsync(new("operator-at-two")).ConfigureAwait(false);
            await fixture.Model.StageProjectionAsync(new("operator-at-two", ProjectionState.FromJsonElement("counter", "tenant-a",
                JsonSerializer.SerializeToElement(new { count = 2 })))
                { SelectedReplayFence = new(fixture.Identity.ActorId, "counter", "operator-at-two", 2) }).ConfigureAwait(false);
            await fixture.Model.PromoteProjectionAsync(new("operator-at-two")).ConfigureAwait(false);
            await fixture.Model.FinalizeProjectionAsync(new("operator-at-two")).ConfigureAwait(false);
            await fixture.LifecycleActor.CompleteRebuildAsync(new("operator-at-two")).ConfigureAwait(false);
            (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(false)).State.ShouldBeNull();
        };
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(2);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(0);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery!.HeadSequence.ShouldBe(1);
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(771)]
    [InlineData(10_000)]
    public async Task LegacyFallback_ProductionActorsReplayCompletePrefixOnceAndPersistScopedCompletion(int head)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(head).ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.DeliverAsync().ConfigureAwait(true);
        cost.Dispose();
        ProjectionState? selected = await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true);
        ProjectionDeliveryState? row = (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State;
        if (head == 0)
        {
            selected.ShouldBeNull(); row.ShouldBeNull(); fixture.Http.Preparations.ShouldBeEmpty();
            cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(0);
            fixture.ModelState.CommittedState.ShouldBeEmpty();
            return;
        }
        selected.ShouldNotBeNull().GetState().GetProperty("count").GetInt32().ShouldBe(head);
        row.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(head);
        row.ActiveReservation.ShouldBeNull();
        row.CompletedMessageIdDigests!.Count.ShouldBe(head);
        row.LastCompletedMessageId.ShouldBe(fixture.Events[^1].MessageId);
        fixture.Http.Preparations.Count.ShouldBe(1);
        fixture.Http.Preparations.Single().ShouldBe(Enumerable.Range(1, head).Select(static value => (long)value));
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(2L * ((head + 255) / 256));
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(2L * head);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.ActivationReadBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.ActivationWriteBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.WireStreamingFallbacks].ShouldBe(0);
        cost[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(8);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBeLessThanOrEqualTo(4);
        cost[ProjectionDeliveryCost.ProofVisits].ShouldBeLessThanOrEqualTo(24L * (head + Math.Min(head, 256) + head));
        (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("promote")]
    [InlineData("release")]
    [InlineData("cancel")]
    public async Task LegacyFallback_InterruptedOriginalWorkResumesWithoutAnotherPreparationOrLogicalPublication(string boundary)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        using var cancel = new CancellationTokenSource();
        fixture.LoseStageResponse = boundary == "stage";
        fixture.LosePromotionResponse = boundary == "promote";
        fixture.LoseReleaseResponse = boundary == "release";
        if (boundary == "cancel") fixture.AfterStage = () => { cancel.Cancel(); return Task.CompletedTask; };
        if (boundary == "cancel") await Should.ThrowAsync<OperationCanceledException>(() => fixture.DeliverAsync(cancel.Token)).ConfigureAwait(true);
        else await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionActivationWorkItem frozen = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        ProjectionLegacyDeliveryIntent intent = frozen.LegacyDelivery.ShouldNotBeNull();
        fixture.Http.Preparations.Count.ShouldBe(1);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.Http.Preparations.Count.ShouldBe(1);
        (await fixture.Model.InspectProjectionRebuildAsync(new(intent.OperationId)).ConfigureAwait(true)).Status
            .ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(257);
        ProjectionDeliveryState row = (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State.ShouldNotBeNull();
        row.LastDeliveredSequence.ShouldBe(257); row.ActiveReservation.ShouldBeNull();
        row.LastCompletedMessageId.ShouldBe(intent.HeadMessageId);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task LegacyFallback_AppendDuringStageRetainsFrozenHeadAndNewerActivationThenDeliversNextHead()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(256).ConfigureAwait(true);
        fixture.AfterStage = async () =>
        {
            await fixture.AppendAsync(1).ConfigureAwait(false);
            await fixture.Outbox.EnsureAsync(fixture.Identity).ConfigureAwait(false);
        };
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(256);
        ProjectionActivationWorkItem next = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
        next.LegacyDelivery.ShouldBeNull(); next.Revision.ShouldBe(2);
        await fixture.DeliverAsync().ConfigureAwait(true);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(257);
        fixture.Http.Preparations.Select(static item => item.Length).ShouldBe([256, 257]);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(257);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task LegacyFallback_RepeatedCompletedTriggerReadsExactSelectedProofWithoutAnotherPublication()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2).ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        await fixture.Outbox.EnsureAsync(fixture.Identity).ConfigureAwait(true);
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.Http.Preparations.Count.ShouldBe(2);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }
}
