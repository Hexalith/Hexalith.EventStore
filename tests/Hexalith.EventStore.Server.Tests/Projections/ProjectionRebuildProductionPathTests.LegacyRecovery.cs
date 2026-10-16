using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class ProjectionRebuildProductionPathTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFallback_CompletedRowWithFailedOutboxCleanupRetainsProofAndReportsPendingRecovery(bool failActivationRemoval)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2).ConfigureAwait(true);
        bool failed = false;
        fixture.AfterPromotion = () =>
        {
            fixture.Backend.BeforeTrySaveByteState = (key, body) =>
            {
                if (!key.StartsWith("projection-activation:outbox:v1:", StringComparison.Ordinal)) return;
                using JsonDocument ledger = JsonDocument.Parse(body);
                JsonElement items = ledger.RootElement.GetProperty("items");
                bool cleanup = failActivationRemoval ? items.GetArrayLength() == 0
                    : items.GetArrayLength() == 1 && !items[0].TryGetProperty("legacyDelivery", out _);
                if (!cleanup) return;
                failed = true;
                throw new IOException("private-cleanup-provider-failure");
            };
            return Task.CompletedTask;
        };
        var outcomes = new List<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name == ProjectionDeliveryDiagnostics.CounterName) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (tags.ToArray().Any(static item => item.Key == ProjectionDeliveryDiagnostics.ModeTag && Equals(item.Value, "full_replay")))
                outcomes.Add(tags.ToArray());
        });
        listener.Start();
        await fixture.DeliverAsync().ConfigureAwait(true);
        failed.ShouldBeTrue();
        KeyValuePair<string, object?>[] pendingOutcome = outcomes.ShouldHaveSingleItem();
        pendingOutcome.Single(static item => item.Key == ProjectionDeliveryDiagnostics.StatusTag).Value.ShouldBe("retryable");
        pendingOutcome.Single(static item => item.Key == ProjectionDeliveryDiagnostics.ReasonTag).Value.ShouldBe("delivery_state_unavailable");
        ProjectionDeliveryStateReadResult completed = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        completed.State!.LastDeliveredSequence.ShouldBe(2);
        completed.State.ActiveReservation.ShouldBeNull();
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(2);
        (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Idle);
        ProjectionActivationWorkItem retained = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!;
        if (failActivationRemoval) retained.LegacyDelivery.ShouldBeNull();
        else retained.LegacyDelivery.ShouldNotBeNull();
        fixture.Backend.BeforeTrySaveByteState = null!;
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        using ProjectionDeliveryCostScope recovery = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.DeliverAsync().ConfigureAwait(true);
        recovery[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        ProjectionDeliveryStateReadResult recovered = await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true);
        recovered.Etag.ShouldBe(completed.Etag);
        JsonSerializer.Serialize(recovered.State).ShouldBe(JsonSerializer.Serialize(completed.State));
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task LegacyFallback_NotificationFailureSettlesPublicationWithOnlyBoundedUnknownRouteOutcome()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(2).ConfigureAwait(true);
        fixture.Notifications.NotifyProjectionChangedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("private-provider-message")));
        var outcomes = new List<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name == ProjectionDeliveryDiagnostics.CounterName) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (tags.ToArray().Any(static item => item.Key == ProjectionDeliveryDiagnostics.ReasonTag && Equals(item.Value, "notification_unavailable")))
                outcomes.Add(tags.ToArray());
        });
        listener.Start();
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.ActorLogger.ReceivedCalls().ShouldBeEmpty();
        KeyValuePair<string, object?>[] failure = outcomes.ShouldHaveSingleItem();
        failure.Length.ShouldBe(4);
        failure.Single(static item => item.Key == ProjectionDeliveryDiagnostics.RouteTag).Value.ShouldBe("unknown");
        failure.Single(static item => item.Key == ProjectionDeliveryDiagnostics.ModeTag).Value.ShouldBe("full_replay");
        failure.ShouldAllBe(static item => item.Value != null && item.Value.GetType() == typeof(string) && ((string)item.Value).Length <= 64);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(2);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task LegacyFallback_AnchorAbsentPrefixMutationDuringPromotionRetainsOldCheckpointAndPendingOriginalWork()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        await fixture.SourceState.SetStateAsync(fixture.Identity.MetadataKey,
            new AggregateMetadata(257, fixture.Clock.GetUtcNow(), null)).ConfigureAwait(true);
        await fixture.SourceState.SaveStateAsync().ConfigureAwait(true);
        fixture.AfterPromotion = async () =>
        {
            await fixture.SourceState.SetStateAsync(fixture.Identity.EventStreamKeyPrefix + 2,
                fixture.Events[1] with { UserId = "changed-during-promotion" }).ConfigureAwait(false);
            await fixture.SourceState.SaveStateAsync().ConfigureAwait(false);
        };
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.DeliverAsync().ConfigureAwait(true);
        cost.Dispose();
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(6);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(3 * 257);
        ProjectionDeliveryState pending = (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!;
        pending.LastDeliveredSequence.ShouldBe(0);
        pending.ActiveReservation.ShouldNotBeNull();
        ProjectionLegacyDeliveryIntent intent = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery!;
        (await fixture.Model.InspectProjectionRebuildAsync(new(intent.OperationId)).ConfigureAwait(true)).Status
            .ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        intent.SourceProof.ShouldBeNull();
        fixture.Http.Preparations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LegacyFallback_OriginalPromotionReceiptResolvesLostResponseWithoutRepreparing()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        fixture.LosePromotionResponse = true;
        fixture.FailReadbackAfterPromotionLoss = false;
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.Http.Preparations.Count.ShouldBe(1);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(257);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFallback_IndependentWorkersRespectOriginalIntentClaimAndExpiredWorkerCannotRepublish(bool expireFirstClaim)
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        ProjectionActivationWorkItem sampled = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!;
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Http.BeforePreparation = async () =>
        {
            paused.SetResult();
            await resume.Task.ConfigureAwait(false);
        };
        Task first = fixture.DeliverReplicaAsync(sampled);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        ProjectionActivationWorkItem claimed = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!;
        ProjectionLegacyDeliveryIntent frozen = claimed.LegacyDelivery!;
        if (expireFirstClaim) fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.DeliverReplicaAsync(expireFirstClaim ? claimed : sampled).ConfigureAwait(true);
        if (expireFirstClaim)
        {
            (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(257);
            (await fixture.Model.InspectProjectionRebuildAsync(new(frozen.OperationId)).ConfigureAwait(true)).Status
                .ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        }
        else
        {
            fixture.Http.Preparations.ShouldBeEmpty();
            (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true)).ShouldBeNull();
            (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery.ShouldBe(frozen);
        }
        resume.SetResult();
        await first.ConfigureAwait(true);
        fixture.Http.Preparations.Count.ShouldBe(expireFirstClaim ? 2 : 1);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(257);
        (await fixture.Model.ReadProjectionStateAsync().ConfigureAwait(true))!.GetState().GetProperty("count").GetInt32().ShouldBe(257);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task LegacyFallback_OneFinalizationDeadlineRetainsCommittedOriginalWorkUntilRecovery()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        fixture.AfterPromotion = () =>
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(31));
            return Task.CompletedTask;
        };
        await fixture.DeliverAsync().ConfigureAwait(true);
        ProjectionDeliveryState pending = (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!;
        pending.LastDeliveredSequence.ShouldBe(0);
        pending.ActiveReservation.ShouldNotBeNull();
        (await fixture.LifecycleActor.ReadSnapshotAsync().ConfigureAwait(true)).Phase.ShouldBe(ProjectionLifecyclePhase.Delivering);
        ProjectionLegacyDeliveryIntent original = (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true))!.LegacyDelivery!;
        (await fixture.Model.InspectProjectionRebuildAsync(new(original.OperationId)).ConfigureAwait(true)).Status
            .ShouldBe(ProjectionRebuildInspectionStatus.Promoted);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        await fixture.DeliverAsync().ConfigureAwait(true);
        fixture.Http.Preparations.Count.ShouldBe(1);
        (await fixture.Rows.ReadAsync(fixture.Identity, "counter").ConfigureAwait(true)).State!.LastDeliveredSequence.ShouldBe(257);
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldBeNull();
        await fixture.Notifications.Received(1).NotifyProjectionChangedAsync("counter", "tenant-a",
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task LegacyFallback_ValidationFailureExportsBoundedUnknownRouteWithoutPayloadOrExceptionLogging()
    {
        using var fixture = new LegacyReplayProductionHarness();
        await fixture.InitializeAsync(257).ConfigureAwait(true);
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        fixture.AfterStage = async () =>
        {
            await fixture.SourceState.SetStateAsync(fixture.Identity.EventStreamKeyPrefix + 2,
                fixture.Events[1] with { UserId = secret }).ConfigureAwait(false);
            await fixture.SourceState.SaveStateAsync().ConfigureAwait(false);
        };
        var outcomes = new List<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name == ProjectionDeliveryDiagnostics.CounterName) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (tags.ToArray().Any(static item => item.Key == ProjectionDeliveryDiagnostics.ModeTag && Equals(item.Value, "full_replay")))
                outcomes.Add(tags.ToArray());
        });
        listener.Start();
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.DeliverAsync().ConfigureAwait(true);
        cost.Dispose();
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        cost[ProjectionDeliveryCost.ActivationWriteBytes].ShouldBeGreaterThan(0);
        KeyValuePair<string, object?>[] outcome = outcomes.ShouldHaveSingleItem();
        outcome.Length.ShouldBe(4);
        outcome.Single(static item => item.Key == ProjectionDeliveryDiagnostics.RouteTag).Value.ShouldBe("unknown");
        outcome.Single(static item => item.Key == ProjectionDeliveryDiagnostics.StatusTag).Value.ShouldBe("failed");
        outcome.Single(static item => item.Key == ProjectionDeliveryDiagnostics.ReasonTag).Value.ShouldBe("source_conflict");
        outcome.ShouldAllBe(static item => item.Value != null && item.Value.GetType() == typeof(string) && ((string)item.Value).Length <= 64);
        foreach (NSubstitute.Core.ICall call in fixture.Logger.ReceivedCalls())
        {
            call.GetArguments().ShouldAllBe(static value => value == null || !typeof(Exception).IsAssignableFrom(value.GetType()));
            call.GetArguments().ShouldAllBe(value => value == null || !value.ToString()!.Contains(secret, StringComparison.Ordinal));
        }
        (await fixture.Outbox.GetAsync(fixture.Identity).ConfigureAwait(true)).ShouldNotBeNull();
    }
}
