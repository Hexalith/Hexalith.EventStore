using System.Text.Json;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionRebuildProductionPathTests
{
    [Theory]
    [InlineData("missing-receipt")]
    [InlineData("pending-candidate")]
    [InlineData("empty-operation")]
    [InlineData("unfinished-receipt")]
    [InlineData("missing-binding")]
    [InlineData("missing-fence")]
    [InlineData("receipt-operation")]
    [InlineData("fence-operation")]
    [InlineData("fence-source")]
    [InlineData("fence-projection")]
    [InlineData("fence-head")]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("projection")]
    [InlineData("head")]
    [InlineData("head-message")]
    [InlineData("source-digest")]
    [InlineData("candidate-digest")]
    [InlineData("missing-floor")]
    [InlineData("different-floor")]
    [InlineData("missing-selected-digest")]
    [InlineData("selected-digest")]
    [InlineData("missing-model")]
    public async Task LegacyFallback_UnactivatedDuplicateRequiresCompletePersistedPublicationProof(string corruption)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false);
        await fixture.DeliverAsync();
        string receiptKey = EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey;
        var receipt = (ProjectionRebuildPromotionReceipt)fixture.ModelState.CommittedState[receiptKey];
        ProjectionReplayPublicationBinding binding = receipt.PublicationBinding!;
        ProjectionReplaySelectionFence fence = receipt.SelectedReplayFence!;
        if (corruption == "missing-receipt") await fixture.ModelState.RemoveStateAsync(receiptKey);
        else if (corruption == "missing-model") await fixture.ModelState.RemoveStateAsync(EventReplayProjectionActor.ProjectionStateKey);
        else if (corruption == "pending-candidate")
            await fixture.ModelState.SetStateAsync(EventReplayProjectionActor.ProjectionRebuildCandidateKey,
                new ProjectionRebuildCandidate("other-operation", (await fixture.Model.ReadProjectionStateAsync())!));
        else
        {
            receipt = corruption switch
            {
                "unfinished-receipt" => receipt with { Completed = false, PromotedState = await fixture.Model.ReadProjectionStateAsync() },
                "missing-binding" => receipt with { PublicationBinding = null },
                "empty-operation" => receipt with { OperationId = string.Empty, PublicationBinding = binding with { OperationId = string.Empty },
                    PublicationFloor = binding with { OperationId = string.Empty }, SelectedReplayFence = fence with { OperationId = string.Empty } },
                "missing-fence" => receipt with { SelectedReplayFence = null },
                "receipt-operation" => receipt with { OperationId = "other-operation" },
                "fence-operation" => receipt with { SelectedReplayFence = fence with { OperationId = "other-operation" } },
                "fence-source" => receipt with { SelectedReplayFence = fence with { SourceActorId = "tenant-a:other:counter-1" } },
                "fence-projection" => receipt with { SelectedReplayFence = fence with { ProjectionName = "other" } },
                "fence-head" => receipt with { SelectedReplayFence = fence with { HeadSequence = 1 } },
                "tenant" => receipt with { PublicationBinding = binding with { TenantId = "tenant-b" } },
                "domain" => receipt with { PublicationBinding = binding with { Domain = "other" } },
                "aggregate" => receipt with { PublicationBinding = binding with { AggregateId = "other" } },
                "projection" => receipt with { PublicationBinding = binding with { ProjectionName = "other" } },
                "head" => receipt with { PublicationBinding = binding with { HeadSequence = 1 } },
                "head-message" => receipt with { PublicationBinding = binding with { HeadMessageId = "other-message" } },
                "source-digest" => receipt with { PublicationBinding = binding with { SourceFingerprint = new string('b', 64) } },
                "candidate-digest" => receipt with { PublicationBinding = binding with { CandidateFingerprint = new string('b', 64) } },
                "missing-floor" => receipt with { PublicationFloor = null },
                "different-floor" => receipt with { PublicationFloor = binding with { HeadSequence = 1 } },
                "missing-selected-digest" => receipt with { SelectedStateFingerprint = null },
                "selected-digest" => receipt with { SelectedStateFingerprint = new string('b', 64) },
                _ => throw new InvalidOperationException(),
            };
            await fixture.ModelState.SetStateAsync(receiptKey, receipt);
        }
        await fixture.ModelState.SaveStateAsync();
        string before = JsonSerializer.Serialize(fixture.ModelState.CommittedState);
        int saves = fixture.ModelState.CommittedSnapshots.Count;
        int stages = fixture.StagedPublications;
        await fixture.Outbox.EnsureAsync(fixture.Identity);
        await fixture.DeliverAsync();
        (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
        JsonSerializer.Serialize(fixture.ModelState.CommittedState).ShouldBe(before);
        fixture.ModelState.CommittedSnapshots.Count.ShouldBe(saves);
        fixture.StagedPublications.ShouldBe(stages);
        (await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).ReadDeliveredSequenceAsync(fixture.Identity, "counter")).ShouldBe(2);
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("incarnation")]
    [InlineData("floor")]
    [InlineData("prefix")]
    [InlineData("head-message")]
    [InlineData("checkpoint")]
    [InlineData("missing-source-proof")]
    [InlineData("source-proof-digest")]
    [InlineData("source-proof-head-message")]
    [InlineData("guarded")]
    [InlineData("schema")]
    public async Task LegacyFallback_UnactivatedDuplicateRechecksSourceAndScopeUnderLifecycleLease(string change)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false);
        await fixture.DeliverAsync();
        await fixture.Outbox.EnsureAsync(fixture.Identity);
        fixture.BeforeLease = async () =>
        {
            if (change == "checkpoint")
            {
                await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).SaveDeliveredSequenceAsync(fixture.Identity, "counter", 3);
                return;
            }
            if (change is "guarded" or "schema")
            {
                ProjectionDeliveryState row = ProjectionDeliveryState.CreateEmpty(fixture.Identity, "counter",
                    ProjectionDeliveryFingerprint.ComputeInitial(fixture.Identity, "counter"), fixture.Clock.GetUtcNow())
                    with { SchemaVersion = change == "guarded" ? ProjectionDeliveryState.GuardSchemaVersion : 99 };
                (await fixture.Rows.TrySaveAsync(fixture.Identity, "counter", row, string.Empty)).ShouldBeTrue();
                return;
            }
            if (change == "incarnation")
            {
                SourceProof proof = SourceProofCodec.CreateSeed(fixture.Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAW");
                foreach (EventEnvelope item in fixture.Events) proof = SourceProofCodec.Extend(proof, item);
                await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
                    new AggregateMetadata(2, fixture.Clock.GetUtcNow(), null) { SourceProof = proof });
            }
            else if (change == "floor")
                await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
                    new AggregateMetadata(2, fixture.Clock.GetUtcNow(), null, 2) { SourceProof = fixture.Head with { RetainedFloor = 2 } });
            else if (change == "missing-source-proof")
                await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
                    new AggregateMetadata(2, fixture.Clock.GetUtcNow(), null));
            else if (change is "source-proof-digest" or "source-proof-head-message")
                await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
                    new AggregateMetadata(2, fixture.Clock.GetUtcNow(), null)
                    { SourceProof = change == "source-proof-digest" ? fixture.Head with { CumulativeDigest = new string('b', 64) }
                        : fixture.Head with { HeadMessageId = "other-head" } });
            else
                await fixture.SourceState.SetStateAsync(fixture.Identity.EventStreamKeyPrefix + 2,
                    change == "prefix" ? fixture.Events[1] with { UserId = "changed" }
                        : fixture.Events[1] with { MessageId = "other-head" });
            await fixture.SourceState.SaveStateAsync();
        };
        string before = JsonSerializer.Serialize(fixture.ModelState.CommittedState);
        await fixture.DeliverAsync();
        (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
        JsonSerializer.Serialize(fixture.ModelState.CommittedState).ShouldBe(before);
        fixture.StagedPublications.ShouldBe(1);
        fixture.BoundReconciliations.ShouldBe(0);
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("storage")]
    [InlineData("ownership")]
    [InlineData("cancel")]
    [InlineData("deadline")]
    public async Task LegacyFallback_UnactivatedDuplicateRetainsOriginalWorkWhenReconciliationCannotComplete(string boundary)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2, writerActive: false);
        await fixture.DeliverAsync();
        await fixture.Outbox.EnsureAsync(fixture.Identity);
        using var cancellation = new CancellationTokenSource();
        fixture.PublicationMethodUnavailable = boundary == "capability";
        fixture.BeforeReconciliation = async (request, _) =>
        {
            if (boundary == "storage")
            {
                string read = "TryGetState:" + EventReplayProjectionActor.ProjectionRebuildPromotionReceiptKey;
                fixture.ModelState.FaultOnCall(read, fixture.ModelState.Trace.Count(item => item == read) + 1, new IOException("unavailable"));
            }
            if (boundary == "ownership")
            {
                await fixture.LifecycleActor.CompleteDeliveryWriteAsync(new(request.LifecycleOperationId));
                await fixture.LifecycleActor.BeginDeliveryWriteAsync(new("other-worker"));
            }
            if (boundary == "cancel") cancellation.Cancel();
            if (boundary == "deadline") fixture.Clock.Advance(TimeSpan.FromSeconds(31));
        };
        string before = JsonSerializer.Serialize(fixture.ModelState.CommittedState);
        if (boundary == "cancel") await Should.ThrowAsync<OperationCanceledException>(() => fixture.DeliverAsync(cancellation.Token));
        else await fixture.DeliverAsync();
        ProjectionLegacyDeliveryIntent retained = (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull().LegacyDelivery.ShouldNotBeNull();
        JsonSerializer.Serialize(fixture.ModelState.CommittedState).ShouldBe(before);
        fixture.StagedPublications.ShouldBe(1);
        fixture.ReleasedWrites.ShouldBe(0);
        (await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).ReadDeliveredSequenceAsync(fixture.Identity, "counter")).ShouldBe(2);
        if (boundary == "ownership") await fixture.LifecycleActor.CompleteDeliveryWriteAsync(new("other-worker"));
        fixture.PublicationMethodUnavailable = false;
        fixture.BeforeReconciliation = null;
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.DeliverAsync();
        (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldBeNull();
        fixture.StagedPublications.ShouldBe(1);
        JsonSerializer.Serialize(fixture.ModelState.CommittedState).ShouldBe(before);
        retained.HeadSequence.ShouldBe(2);
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LegacyFallback_UnactivatedDuplicateRetainsNewerActivationAndThenDeliversItsHead()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(1, writerActive: false);
        await fixture.DeliverAsync();
        await fixture.Outbox.EnsureAsync(fixture.Identity);
        fixture.BeforeLease = async () =>
        {
            await fixture.AppendAsync(1);
            await fixture.Outbox.EnsureAsync(fixture.Identity);
        };
        string selected = JsonSerializer.Serialize(fixture.ModelState.CommittedState);
        await fixture.DeliverAsync();
        JsonSerializer.Serialize(fixture.ModelState.CommittedState).ShouldBe(selected);
        ProjectionActivationWorkItem retained = (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldNotBeNull();
        retained.LegacyDelivery.ShouldBeNull();
        await fixture.DeliverAsync();
        (await ((IProjectionDeliveryCheckpointStore)fixture.Checkpoints).ReadDeliveredSequenceAsync(fixture.Identity, "counter")).ShouldBe(2);
        (await fixture.Model.ReadProjectionStateAsync())!.GetState().GetProperty("count").GetInt32().ShouldBe(2);
        (await fixture.Outbox.GetAsync(fixture.Identity)).ShouldBeNull();
        await fixture.Notifications.Received(2).NotifyProjectionChangedAsync("counter", "tenant-a", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
