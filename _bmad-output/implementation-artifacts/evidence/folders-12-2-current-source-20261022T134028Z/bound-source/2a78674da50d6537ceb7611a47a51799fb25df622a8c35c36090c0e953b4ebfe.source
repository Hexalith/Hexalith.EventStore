using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GuardedPreparation_InterruptedReferenceReusesTheFirstImmutableManifest(bool referencePersisted, bool historicalManifest)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            handlerSemantics: "full-replay-v1");
        string? manifestKey = null;
        fixture.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (!key.EndsWith(":-3", StringComparison.Ordinal)) return;
            manifestKey = key[..^1] + "2";
            if (referencePersisted) fixture.Backend.SeedByteStore(key, bytes);
            throw new IOException("Interrupted reference save.");
        };
        await Should.ThrowAsync<IOException>(() => fixture.StagedCanonicalReplayAsync("stage"));
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Backend.BeforeTrySaveByteState = null!;
        if (historicalManifest)
        {
            ReadModelEntry<byte[]> persisted = await fixture.Models.GetAsync<byte[]>("models", manifestKey!);
            (await fixture.Models.TrySaveAsync("models", manifestKey!, DomainProjectionGuardedReplay.OriginalManifest(persisted.Value!, null).ToArray(),
                persisted.ETag!)).ShouldBeTrue();
        }
        foreach (string action in new[] { "stage", "commit", "verify" })
            (await fixture.StagedCanonicalReplayAsync(action)).Outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull().Count.ShouldBe(3);
    }

    [Fact]
    public async Task GuardedPreparation_FirstWriteLostResponseRetainsItsRequestBindingAndNeverPreparesAgain()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            handlerSemantics: "full-replay-v1");
        byte[]? original = null;
        fixture.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (!key.EndsWith(":-2", StringComparison.Ordinal)) return;
            original = bytes.ToArray();
            fixture.Backend.SeedByteStore(key, bytes);
            throw new IOException("Lost original manifest response.");
        };
        await Should.ThrowAsync<IOException>(() => fixture.StagedCanonicalReplayAsync("stage"));
        fixture.Backend.BeforeTrySaveByteState = null!;
        var conflict = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => fixture.StagedCanonicalReplayAsync("stage", changedInput: true));
        conflict.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await fixture.StagedCanonicalReplayAsync("stage")).Outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        original.ShouldNotBeNull();
    }

    [Fact]
    public async Task GuardedPreparation_CompletedCleanupRetainsEvidenceThatFencesDelayedStage()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            handlerSemantics: "full-replay-v1");
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Http.ReplayPreparationKeys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key) == key.EndsWith(":-3", StringComparison.Ordinal));
        var delayed = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => fixture.StagedCanonicalReplayAsync("stage"));
        delayed.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        fixture.Work.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedRetry_SupersededUnreservedWorkRequiresItsExactCompletedMessageId(bool conflictingId)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3);
        await fixture.UseProductionOwnersAsync();
        string message = conflictingId ? "different-original-message" : fixture.Events[1].MessageId;
        var original = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, 2), fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, fixture.Catalog.AppId, fixture.Catalog.ServiceVersion, 2, message,
            fixture.Catalog.OrdinaryProjectionTypes, [], message, fixture.Catalog.CatalogFingerprint, 0, fixture.Clock.GetUtcNow(), null);
        await fixture.RunGuardedRetryAsync(original);
        if (conflictingId) fixture.Work.ShouldNotBeNull().DispatchId.ShouldBe(message);
        else fixture.Work.ShouldBeNull();
        fixture.RowWrites.ShouldBe(0);
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 0 && handler.RebuildCalls == 0);
        fixture.ReturnedSequences.ShouldBeEmpty();
    }

    [Fact]
    public async Task GuardedRetry_OwningSchedulerSettlesOriginalWakeupAfterNewerHeadCompletion()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, productionScheduler: true);
        await fixture.UseProductionOwnersAsync();
        string message = fixture.Events[1].MessageId;
        var original = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, 2), fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, fixture.Catalog.AppId, fixture.Catalog.ServiceVersion, 2, message,
            fixture.Catalog.OrdinaryProjectionTypes, [], message, fixture.Catalog.CatalogFingerprint, 0, fixture.Clock.GetUtcNow(), null);
        await fixture.Scheduler.ScheduleAsync(original);
        await fixture.RunPersistedRetryWorkerAsync();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        var pending = await fixture.Scheduler.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64);
        pending.ShouldHaveSingleItem().WorkId.ShouldBe(original.WorkId);
        pending.Single().ReservationFencingTokens.ShouldBeEmpty();
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        await fixture.RunPersistedRetryWorkerAsync();
        (await fixture.Scheduler.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64)).ShouldBeEmpty();
        fixture.Handlers.Single().Calls.ShouldBe(2);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(0);
        fixture.Rows.Values.Single().LastCompletedMessageId.ShouldBe(fixture.Head.HeadMessageId);
    }

    [Fact]
    public async Task GuardedPendingRecovery_SlowReadOnlyHistoryCannotConsumeItsFinalizationDeadline()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1);
        await fixture.UseProductionOwnersAsync();
        fixture.AfterDeliveryReleaseAsync = () => Task.FromException(new IOException("Lost release response."));
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Rows.Values.Single().IdleBindingPending.ShouldNotBeNull();
        int calls = fixture.Handlers.Single().Calls;
        fixture.HistoryReadDelay = TimeSpan.FromSeconds(31);
        DateTimeOffset began = fixture.Clock.GetUtcNow();
        ProjectionSourceGuardDeliveryResult recovered = await fixture.DeliverAsync(256);
        recovered.ReasonCode.ShouldBe("delivery_already_completed");
        (fixture.Clock.GetUtcNow() - began).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(31));
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        fixture.Rows.Values.Single().IdleBindingPending.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(calls);
        await fixture.RunGuardedRetryAsync();
        fixture.Work.ShouldBeNull();
    }
}
