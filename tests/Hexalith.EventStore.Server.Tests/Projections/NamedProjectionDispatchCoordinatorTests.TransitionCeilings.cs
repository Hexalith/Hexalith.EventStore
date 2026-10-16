using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(8, "reserve")]
    [InlineData(32, "reserve")]
    [InlineData(8, "pending")]
    [InlineData(32, "pending")]
    [InlineData(8, "complete")]
    [InlineData(32, "complete")]
    public async Task GuardedOwningTransitionsExhaustExactlyTheConfiguredCapThenPreserveOriginalWorkAndUseFencedRecovery(int attempts, string transition)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            transitionAttempts: attempts).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route);
        int refused = 0;
        fixture.Backend.BeforeTrySaveByteState = (savedKey, bytes) =>
        {
            if (savedKey != key) return;
            ProjectionDeliveryState row = JsonSerializer.Deserialize<ProjectionDeliveryState>(bytes.Span, JsonSerializerOptions.Web)!;
            bool chosen = transition switch
            {
                "reserve" => row.ActiveReservation is not null && row.IdleBindingPending is null,
                "pending" => row.IdleBindingPending is not null,
                _ => row.LastDeliveredSequence == 3,
            };
            if (!chosen) return;
            refused++;
            throw new IOException("Fixture conditional write unavailable.");
        };
        Dictionary<string, long> measured;
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
            result.AlreadyCurrent.ShouldBeFalse();
            result.ReasonCode.ShouldBe(transition == "reserve" ? "delivery_state_unavailable" : "delivery_reconciliation_required");
            refused.ShouldBe(attempts);
            cost[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(3L * attempts + 5);
            cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBeLessThanOrEqualTo(3L * attempts + 1);
            cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2 * cost[ProjectionDeliveryCost.ProofReads]);
            const long size = 16_384 + 80 * 3 + 512 * 3;
            cost[ProjectionDeliveryCost.ProofReadBytes].ShouldBeLessThanOrEqualTo((3L * attempts + 5) * size);
            cost[ProjectionDeliveryCost.ProofWriteBytes].ShouldBeLessThanOrEqualTo((3L * attempts + 1) * size);
            cost[ProjectionDeliveryCost.MarkerReadBytes].ShouldBeLessThanOrEqualTo(2L * (3L * attempts + 5) * 4096);
            cost[ProjectionDeliveryCost.ProofVisits].ShouldBeLessThanOrEqualTo((8L * attempts + 16) * (3 + 3 + 2));
            cost[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
            cost[ProjectionDeliveryCost.SourceRanges].ShouldBeLessThanOrEqualTo(3);
            cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBeLessThanOrEqualTo(6);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(transition == "reserve" ? 0 : 2);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(transition == "reserve" ? 0 : 4);
            measured = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => cost[category]);
        }
        ProjectionDeliveryState pending = fixture.Rows[route];
        pending.LastDeliveredSequence.ShouldBe(1);
        pending.CompletedMessageIdDigests!.Count.ShouldBe(1);
        (pending.ActiveReservation is null).ShouldBe(transition == "reserve");
        (pending.IdleBindingPending is not null).ShouldBe(transition == "complete");
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        original.DispatchId.ShouldBe(fixture.Head.HeadMessageId);
        original.PendingRoutes.ShouldBe([route]);
        fixture.Backend.BeforeTrySaveByteState = null!;
        ProjectionSourceGuardDeliveryResult recovery = await fixture.DeliverAsync(256).ConfigureAwait(true);
        if (transition == "reserve")
        {
            // No reservation persisted before this activation released its
            // lifecycle token. Its old Idle binding cannot adopt that new
            // revision without authenticated reconciliation.
            recovery.AlreadyCurrent.ShouldBeFalse();
            recovery.ReasonCode.ShouldBe("delivery_reconciliation_required");
            fixture.Rows[route].ShouldBe(pending);
            fixture.Work.ShouldBe(original);
            fixture.Handlers.Single().Calls.ShouldBe(0);
            await RepairGuardedCheckpointAsync(fixture, route, pending).ConfigureAwait(true);
            fixture.Work.ShouldBe(original);
            (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        }
        else
        {
            if (transition == "complete")
            {
                // Pending finalization settles the frozen completion first;
                // a later scheduled Current activation owns ledger cleanup.
                recovery.AlreadyCurrent.ShouldBeFalse();
                recovery.ReasonCode.ShouldBe("delivery_already_completed");
                await fixture.RunGuardedRetryAsync().ConfigureAwait(true);
            }
            else recovery.AlreadyCurrent.ShouldBeTrue();
        }
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(3);
        fixture.Rows[route].CompletedMessageIdDigests!.Count.ShouldBe(3);
        fixture.Rows[route].ActiveReservation.ShouldBeNull();
        fixture.Rows[route].IdleBindingPending.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(2);
        fixture.Work.ShouldBeNull();
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionTransitionCapCost " + JsonSerializer.Serialize(new
        { attempts, transition, refused, head = 3, checkpoint = 1, pageSize = 256, cost = measured }));
    }

    private static async Task RepairGuardedCheckpointAsync(GuardedTailProductionHarness fixture, string route, ProjectionDeliveryState pending)
    {
        await fixture.CompleteOperatorRebuildAsync("reconciled-generation", pending.LastDeliveredSequence).ConfigureAwait(false);
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        var maintenance = new ProjectionSourceGuardMaintenance("operator-7", new string('b', 40),
            "consistent-fixture-backup", ProjectionSourceGuard.ApprovedSpecSha256, true, true, true,
            [.. new[] { "append", "immediate", "poller", "retry", "reconciliation", "rebuild", "erasure" }
                .Select(static role => new ProjectionSourceGuardWriterInventory(role, new string('b', 40), "fixture@immutable-sha256", true))]);
        (await fixture.CreateQualification(store).QualifyAsync(fixture.Identity, route, fixture.Head.HeadSequence,
            fixture.Head.SourceIncarnation, maintenance, false).ConfigureAwait(false)).Qualified.ShouldBeTrue();
        ProjectionDeliveryState reconciled = (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(false)).State!;
        reconciled.LastDeliveredSequence.ShouldBe(pending.LastDeliveredSequence);
        reconciled.CompletedReceipts.ShouldBe(pending.CompletedReceipts);
        reconciled.CompletedMessageIdDigests.ShouldBe(pending.CompletedMessageIdDigests);
    }
}
