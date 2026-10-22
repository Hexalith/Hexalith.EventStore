using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData("single-event-v1", "claimed")]
    [InlineData("single-event-v1", "reserved")]
    [InlineData("full-replay-v1", "claimed")]
    [InlineData("full-replay-v1", "reserved")]
    public async Task GuardedOwningCancellationAfterClaimOrReservationPreservesOriginalWorkBeforeAnyHandler(string semantics, string boundary)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            handlerSemantics: semantics, productionScheduler: true).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState originalRow = fixture.Rows[route];
        using var caller = new CancellationTokenSource();
        bool interrupted = false;
        fixture.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (interrupted) return;
            using JsonDocument json = JsonDocument.Parse(bytes);
            bool selected = boundary == "reserved"
                ? key == ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route)
                    && json.RootElement.TryGetProperty("activeReservation", out JsonElement reservation) && reservation.ValueKind == JsonValueKind.Object
                : json.RootElement.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Array
                    && items.EnumerateArray().Any(static item => item.TryGetProperty("leaseOwner", out JsonElement owner) && owner.ValueKind == JsonValueKind.String);
            if (selected) { interrupted = true; caller.Cancel(); }
        };
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            await Should.ThrowAsync<OperationCanceledException>(() => fixture.Probe.TryDeliverAsync(fixture.Identity, 256, caller.Token)).ConfigureAwait(true);
            interrupted.ShouldBeTrue();
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.FoldAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(0);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            cost[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
            cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(boundary == "reserved" ? 1 : 0);
        }
        fixture.Backend.BeforeTrySaveByteState = null!;
        ProjectionDeliveryState pending = fixture.Rows[route];
        pending.LastDeliveredSequence.ShouldBe(1);
        pending.CompletedMessageIdDigests.ShouldBe(originalRow.CompletedMessageIdDigests);
        (pending.ActiveReservation is not null).ShouldBe(boundary == "reserved");
        pending.IdleBindingPending.ShouldBeNull();
        ProjectionDeliveryRetryWorkItem original = (await fixture.Scheduler.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64)
            .ConfigureAwait(true)).Single();
        original.DispatchId.ShouldBe(fixture.Head.HeadMessageId);
        original.PendingRoutes.ShouldBe([route]);
        original.Attempt.ShouldBe(1);
        if (boundary == "claimed") await RepairGuardedCheckpointAsync(fixture, route, pending).ConfigureAwait(true);
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(3);
        fixture.Rows[route].CompletedMessageIdDigests!.Count.ShouldBe(3);
        fixture.Rows[route].ActiveReservation.ShouldBeNull();
        fixture.Rows[route].IdleBindingPending.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(semantics == "single-event-v1" ? 2 : 0);
        fixture.Handlers.Single().RebuildCalls.ShouldBe((semantics == "full-replay-v1" ? 1 : 0) + (boundary == "claimed" ? 1 : 0));
        fixture.Handlers.Single().Dispatches.ShouldAllBe(dispatch => dispatch == original.DispatchId);
        (await fixture.Scheduler.GetDueAsync(fixture.Clock.GetUtcNow().AddHours(1), 64).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("single-event-v1")]
    [InlineData("full-replay-v1")]
    public async Task GuardedOwningLostReleaseResponseAfterPendingCasSettlesOriginalCompletionWithoutRedispatch(string semantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            handlerSemantics: semantics).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        fixture.AfterDeliveryReleaseAsync = () => Task.FromException(new IOException("fixture-lost-release-response"));
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        ProjectionDeliveryState pending = fixture.Rows[route];
        pending.LastDeliveredSequence.ShouldBe(1);
        ProjectionDeliveryReservation reservation = pending.ActiveReservation.ShouldNotBeNull();
        pending.IdleBindingPending.ShouldNotBeNull();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        int calls = fixture.Handlers.Single().Calls, preparations = fixture.Handlers.Single().RebuildCalls;
        using (ProjectionDeliveryCostScope recovery = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
            result.AlreadyCurrent.ShouldBeFalse();
            result.ReasonCode.ShouldBe("delivery_already_completed");
            recovery[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            recovery[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            recovery[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
            recovery[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBeLessThanOrEqualTo(3);
        }
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(reservation.EndSequence);
        fixture.Rows[route].LastCompletedMessageId.ShouldBe(original.HeadMessageId);
        fixture.Rows[route].CompletedMessageIdDigests!.Count.ShouldBe(3);
        fixture.Rows[route].ActiveReservation.ShouldBeNull();
        fixture.Rows[route].IdleBindingPending.ShouldBeNull();
        await fixture.RunGuardedRetryAsync().ConfigureAwait(true);
        fixture.Work.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(calls);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(preparations);
    }
}
