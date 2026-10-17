using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(0, "stage")]
    [InlineData(1, "commit")]
    [InlineData(2, "stage")]
    [InlineData(2, "commit")]
    [InlineData(3, "stage")]
    [InlineData(3, "commit")]
    public async Task GuardedMultiRoute_ExplicitNoEffectFallbackUsesActualRoutesAndOneOriginalPreparation(int fallbackRoute, string? lostAction)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, routeCount: fallbackRoute == 3 ? 3 : 2);
        await fixture.UseProductionOwnersAsync();
        for (int index = 0; index < fixture.Handlers.Count; index++)
            fixture.Handlers[index].RequireRebuildBeforeEffects = fallbackRoute == 2 || fallbackRoute == index || fallbackRoute == 3 && index != 1;
        fixture.Http.LoseReplayResponseAfterAction = lostAction;
        string dispatch = fixture.Head.HeadMessageId!;
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBe(lostAction is null);
        if (lostAction is not null)
        {
            fixture.Work.ShouldNotBeNull().DispatchId.ShouldBe(dispatch);
            fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 1 && row.ActiveReservation != null);
            fixture.ReconstructGuardedDelivery();
            await fixture.RunGuardedRetryAsync();
        }
        fixture.Work.ShouldBeNull();
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 3 && row.ActiveReservation == null && row.IdleBindingPending == null);
        fixture.Http.ReplayPreparationKeys.Count.ShouldBe(2);
        for (int index = 0; index < fixture.Handlers.Count; index++)
        {
            bool replay = fallbackRoute == 2 || fallbackRoute == index || fallbackRoute == 3 && index != 1;
            GuardedTailProjectionHandler handler = fixture.Handlers[index];
            handler.RebuildCalls.ShouldBe(replay ? 1 : 0);
            handler.Calls.ShouldBe(replay ? 1 : 2);
            handler.Dispatches.ShouldAllBe(value => value == dispatch);
            string route = handler.ProjectionType;
            ProjectionDeliveryState row = fixture.Rows[route];
            row.GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.BatchId.ShouldBe(dispatch);
            row.GuardEvidence.OwnerProof.Selection.ReceiptProjectionName.ShouldBe(replay ? "rebuild" : null);
            row.CompletedMessageIdDigests!.Count.ShouldBe(3);
            GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull();
            model.Count.ShouldBe(3);
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")).Value.ShouldBe(model);
        }
        if (lostAction is null)
        {
            costs[ProjectionDeliveryCost.BatchExecutions].ShouldBe(fallbackRoute == 2 ? 1 : 3);
            costs[ProjectionDeliveryCost.ModelMutations].ShouldBe(fallbackRoute == 2 ? 4 : 6);
        }
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Fact]
    public async Task GuardedMultiRoute_InterruptedSecondFallbackSwitchResumesOneOriginalCoordinatedManifest()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, routeCount: 3);
        await fixture.UseProductionOwnersAsync();
        fixture.Handlers[0].RequireRebuildBeforeEffects = true;
        fixture.Handlers[2].RequireRebuildBeforeEffects = true;
        string[] routes = fixture.Catalog.OrdinaryProjectionTypes.ToArray();
        fixture.InterruptBeforeCanonicalRoute = routes[2];
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Rows[routes[0]].ActiveReservation.ShouldNotBeNull().GuardBinding.ShouldNotBeNull().CanonicalReplay.ShouldBeTrue();
        fixture.Rows[routes[2]].ActiveReservation.ShouldNotBeNull().GuardBinding.ShouldNotBeNull().CanonicalReplay.ShouldBeFalse();
        fixture.Http.ReplayPreparationKeys.ShouldBeEmpty();
        fixture.Handlers.ShouldAllBe(handler => handler.RebuildCalls == 0);
        fixture.Handlers[1].Calls.ShouldBe(2);
        fixture.InterruptBeforeCanonicalRoute = null;
        fixture.ReconstructGuardedDelivery();
        await fixture.RunGuardedRetryAsync();
        fixture.Work.ShouldBeNull();
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 3 && row.ActiveReservation == null && row.IdleBindingPending == null);
        fixture.Handlers[0].RebuildCalls.ShouldBe(1);
        fixture.Handlers[2].RebuildCalls.ShouldBe(1);
        fixture.Handlers[1].Calls.ShouldBe(2);
        fixture.Handlers[1].RebuildCalls.ShouldBe(0);
        fixture.Http.ReplayPreparationKeys.Count.ShouldBe(2);
        fixture.Rows[routes[0]].GuardEvidence!.OwnerProof.ReceiptFingerprint.ShouldBe(fixture.Rows[routes[2]].GuardEvidence!.OwnerProof.ReceiptFingerprint);
        foreach (string route in routes)
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull().Count.ShouldBe(3);
    }

    [Fact]
    public async Task GuardedMultiRoute_NonReplayPartialCompletionPersistsOnlyRemainingReservationWithRealScheduler()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, routeCount: 2, productionScheduler: true);
        await fixture.UseProductionOwnersAsync();
        string[] routes = fixture.Catalog.OrdinaryProjectionTypes.ToArray();
        fixture.InterruptBeforePendingRoute = routes[1];
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        string workId = ProjectionDeliveryRetryWorkItem.CreateWorkId(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, 3);
        ProjectionDeliveryRetryWorkItem saved = (await fixture.Scheduler.ReadAsync(workId)).ShouldNotBeNull();
        saved.PendingRoutes.ShouldBe([routes[1]]);
        saved.ReservationFencingTokens.Keys.ShouldBe([routes[1]]);
        saved.ReservationFencingTokens[routes[1]].ShouldBe(fixture.Rows[routes[1]].ActiveReservation.ShouldNotBeNull().FencingToken);
        saved.Revision.ShouldBeGreaterThan(1);
        saved.LeaseOwner.ShouldBeNull();
        fixture.Rows[routes[0]].LastDeliveredSequence.ShouldBe(3);
        fixture.Rows[routes[1]].LastDeliveredSequence.ShouldBe(1);
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 2 && handler.RebuildCalls == 0);
        fixture.InterruptBeforePendingRoute = null;
        fixture.Clock.Advance(TimeSpan.FromSeconds(10));
        fixture.ReconstructGuardedDelivery();
        await fixture.RunPersistedRetryWorkerAsync();
        (await fixture.Scheduler.ReadAsync(workId)).ShouldBeNull();
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 3 && row.ActiveReservation == null && row.IdleBindingPending == null);
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 2 && handler.RebuildCalls == 0);
        foreach (string route in routes)
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull().Count.ShouldBe(3);
    }
}
