using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(0, 256)]
    [InlineData(1, 256)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public async Task GuardedProtectedWirePass_Exact64MiBAndOneByteOverChargeActualDecryptAndSizingBeforeEffects(int over, int pageSize)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(49, 1).ConfigureAwait(true);
        fixture.SetExactProtectedTailWireBytes(67_108_864L + over);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(pageSize).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBe(over == 0);
        cost[ProjectionDeliveryCost.WireReadabilityAttempts].ShouldBe(over == 0 ? 144 : 48);
        cost[ProjectionDeliveryCost.WireUnprotectionAttempts].ShouldBe(fixture.ProtectedReads);
        fixture.ProtectedReads.ShouldBe(over == 0 ? 144 : 48);
        cost[ProjectionDeliveryCost.WireStreamingFallbacks].ShouldBe(0);
        cost[ProjectionDeliveryCost.WireDecodedEvents].ShouldBe(fixture.ProtectedReads);
        cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBeLessThan(67_108_864);
        if (over == 0)
        {
            cost[ProjectionDeliveryCost.WireBytes].ShouldBe(3L * 67_108_864);
            cost[ProjectionDeliveryCost.WireSizingBytes].ShouldBe(3L * 67_108_864);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(48);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(96);
            fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(49);
        }
        else
        {
            result.ReasonCode.ShouldBe("delivery_byte_limit_exceeded");
            cost[ProjectionDeliveryCost.WireSizingBytes].ShouldBe(67_108_865);
            cost[ProjectionDeliveryCost.WireBytes].ShouldBeGreaterThan(0);
            cost[ProjectionDeliveryCost.WireBytes].ShouldBeLessThan(67_108_864);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            fixture.Work.ShouldBeNull();
            fixture.RowWrites.ShouldBe(0);
        }
        ReportProductionCost(over == 0 ? "protected_exact_wire_bytes" : "protected_wire_rejection", 49, 1, pageSize, 256, cost);
    }

    [Fact]
    public async Task GuardedProtectedMixedTail_ChargesActualProviderReadabilityAndKeepsCurrentReadOnly()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(6, 1).ConfigureAwait(true);
        fixture.SetProtectedTailPayloadSizes(1, 1_000_000, 1_048_576, 1024, 1_048_576, 1_000_000);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            fixture.ProtectedReads.ShouldBe(15);
            cost[ProjectionDeliveryCost.WireUnprotectionAttempts].ShouldBe(15);
            cost[ProjectionDeliveryCost.WireDecodedPayloadBytes].ShouldBe(3L * (1_000_000 + 1_048_576 + 1024 + 1_048_576 + 1_000_000));
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(5);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(10);
            ReportProductionCost("protected_mixed_tail", 6, 1, 256, 256, cost);
        }
        using ProjectionDeliveryCostScope current = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.ProtectedReads.ShouldBe(15);
        current[ProjectionDeliveryCost.WireUnprotectionAttempts].ShouldBe(0);
        current[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        current[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
    }

    [Theory]
    [InlineData(false, 1, 2313)]
    [InlineData(true, 256, 12)]
    public async Task GuardedRetryWorker_UsesItsActual256EventRangeProfile(bool retry, int pageSize, int requests)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(9771, 9000).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        if (retry) await fixture.RunGuardedRetryAsync().ConfigureAwait(true);
        else (await fixture.DeliverAsync(1).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Ranges.Count.ShouldBe(requests);
        fixture.Ranges.ShouldAllBe(range => range.PageSize == pageSize && range.After >= 9000 && range.Head == 9771);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(requests);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(2313);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(771);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(1542);
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 9771 && row.ActiveReservation == null
            && row.IdleBindingPending == null && row.GuardEvidence != null && row.GuardEvidence.Source == fixture.Head);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes.Single())
            .ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(9771);
        fixture.Work.ShouldBeNull();
        ReportProductionCost(retry ? "worker_tail" : "immediate_tail", 9771, 9000, pageSize, 256, cost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GuardedSourcePass_Exact64MiBAndOneByteOverPreservePreEffectAdmission(int over)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(34, 1).ConfigureAwait(true);
        const int eventBytes = 2_033_599;
        int[] sizes = [.. Enumerable.Repeat(eventBytes, 32), 67_108_864 - (33 * 2) - (32 * eventBytes) + over];
        fixture.SetTailEnvelopeSizes(1, sizes);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        if (over == 0)
        {
            (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBe(3L * 67_108_864);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(66);
            fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(34);
        }
        else
        {
            ProjectionSourceGuardDeliveryResult rejected = await fixture.DeliverAsync(256).ConfigureAwait(true);
            rejected.AlreadyCurrent.ShouldBeFalse();
            rejected.ReasonCode.ShouldBe("delivery_byte_limit_exceeded");
            cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBe(67_108_865);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            fixture.Work.ShouldBeNull();
            fixture.RowWrites.ShouldBe(0);
            fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        }
        ReportProductionCost(over == 0 ? "tail_exact_source_bytes" : "tail_source_byte_rejection", 34, 1, 256, 256, cost);
    }

    [Fact]
    public async Task GuardedMixedLargePayloadTail_ChargesEveryActualLookaheadDecodeAndSizingByte()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(6, 1).ConfigureAwait(true);
        fixture.SetTailPayloadSizes(1, 1_000_000, 1_048_576, 1024, 1_048_576, 1_000_000);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(12); // greedy byte packing makes four pages per pass
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(15);
        cost[ProjectionDeliveryCost.SourceLookaheadEvents].ShouldBe(9);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(24);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(24);
        cost[ProjectionDeliveryCost.SourceEventHashes].ShouldBe(15);
        cost[ProjectionDeliveryCost.SourceChainExtensions].ShouldBe(15);
        cost[ProjectionDeliveryCost.RouteEventHashes].ShouldBe(15);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(5);
        cost[ProjectionDeliveryCost.SourceLookaheadBytes].ShouldBeGreaterThan(0);
        long returnedEnvelopeBytes = cost[ProjectionDeliveryCost.SourceResponseBytes] - (2 * 12) - (15 - 12);
        cost[ProjectionDeliveryCost.SourceDeserializationBytes].ShouldBe(returnedEnvelopeBytes + cost[ProjectionDeliveryCost.SourceLookaheadBytes]);
        cost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBe(returnedEnvelopeBytes + cost[ProjectionDeliveryCost.SourceLookaheadBytes]);
        cost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBeLessThanOrEqualTo(
            cost[ProjectionDeliveryCost.SourceResponseBytes] + cost[ProjectionDeliveryCost.SourceLookaheadBytes]);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(10);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(6);
        ReportProductionCost("tail_mixed_bytes", 6, 1, 256, 256, cost);
    }

    public static TheoryData<int, int, int> CurrentGuardWorkloads
    {
        get
        {
            var cases = new TheoryData<int, int, int>();
            foreach (int head in new[] { 1, 256, 257, 9999, 10000 })
                foreach (int page in new[] { 1, 16, 256 })
                    foreach (int receipts in new[] { 256, 4096 }) cases.Add(head, receipts, page);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CurrentGuardWorkloads))]
    public async Task GuardedCurrent_ProductionActorsAndStoresMeetExactLogicalReadAndZeroMutationBudget(int head, int receiptLimit, int pageSize)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, head,
            receiptLimit: receiptLimit).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardDeliveryResult observed = await fixture.Probe.TryDeliverAsync(fixture.Identity, pageSize, CancellationToken.None).ConfigureAwait(true);
        observed.AlreadyCurrent.ShouldBeTrue();
        cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(2);
        cost[ProjectionDeliveryCost.ProofReads].ShouldBe(1);
        cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.MetadataReads].ShouldBe(6); // two SDK keys plus two two-key Idle snapshots
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceEventHashes].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(0);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        long s = 16_384L + (80 * head) + (512 * Math.Min(head, receiptLimit));
        cost[ProjectionDeliveryCost.ProofReadBytes].ShouldBeLessThanOrEqualTo(s);
        (cost[ProjectionDeliveryCost.ProofReadBytes] + cost[ProjectionDeliveryCost.MarkerReadBytes]
            + cost[ProjectionDeliveryCost.SourceMetadataBytes] + cost[ProjectionDeliveryCost.MetadataReadBytes])
            .ShouldBeLessThanOrEqualTo(s + 114_688);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(head);
        fixture.RowWrites.ShouldBe(0);
        ReportProductionCost("current", head, head, pageSize, receiptLimit, cost);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public async Task GuardedThreeRouteTail_ProductionOwnersChargeAllSevenRealPassesWithoutPriorHistory(int pageSize)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(10000, 9999, 3).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.Probe.TryDeliverAsync(fixture.Identity, pageSize, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(7);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(7);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(7);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(7);
        cost[ProjectionDeliveryCost.SourceLookaheadEvents].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(3);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(6);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(3);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(9);
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 10000 && row.ActiveReservation == null
            && row.IdleBindingPending == null && row.GuardEvidence != null && row.GuardEvidence.Source == fixture.Head);
        fixture.Work.ShouldBeNull();
        ReportProductionCost("tail", 10000, 9999, pageSize, 256, cost, 3);
    }

    [Theory]
    [InlineData(10000, 9999, 1)]
    [InlineData(10000, 9999, 256)]
    [InlineData(9771, 9000, 256)]
    [InlineData(9771, 9000, 1)]
    [InlineData(257, 1, 256)]
    [InlineData(258, 1, 256)]
    [InlineData(513, 1, 256)]
    [InlineData(514, 1, 256)]
    public async Task GuardedTail_ProductionActorStoreAndHandlerCostsStayInsideEveryIndependentCeiling(int head, int completed, int pageSize)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, completed).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        int delta = head - completed, q = (delta + pageSize - 1) / pageSize;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(pageSize).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        cost[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(3 * q);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.SourceLookaheadEvents].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBeLessThanOrEqualTo(3 + (6 * q));
        cost[ProjectionDeliveryCost.SourceEventHashes].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.SourceChainExtensions].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.RouteEventHashes].ShouldBe(3 * delta);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(delta);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(delta);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2 * delta);
        cost[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(8);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(3);
        cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2 * cost[ProjectionDeliveryCost.ProofReads]);
        long control = 64 + 32 + (16 * delta) + (8 * 2 * delta);
        cost[ProjectionDeliveryCost.MetadataReads].ShouldBeLessThanOrEqualTo(control);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBeLessThanOrEqualTo(control);
        cost[ProjectionDeliveryCost.ProofVisits].ShouldBeGreaterThan(head);
        cost[ProjectionDeliveryCost.ProofVisits].ShouldBeLessThanOrEqualTo((8L + 16) * (head + Math.Min(head, 256) + delta));
        long s = 16_384L + (80 * head) + (512 * Math.Min(head, 256));
        cost[ProjectionDeliveryCost.ProofReadBytes].ShouldBeLessThanOrEqualTo(8 * s);
        cost[ProjectionDeliveryCost.ProofWriteBytes].ShouldBeLessThanOrEqualTo(4 * s);
        cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.WireBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.ModelBytes].ShouldBeGreaterThan(0);
        fixture.RowWrites.ShouldBe(3);
        ProjectionDeliveryState row = fixture.Rows.Values.Single();
        row.LastDeliveredSequence.ShouldBe(head);
        row.CompletedMessageIdDigests!.Count.ShouldBe(head);
        row.ActiveReservation.ShouldBeNull();
        row.IdleBindingPending.ShouldBeNull();
        row.GuardEvidence.ShouldNotBeNull().Source.ShouldBe(fixture.Head);
        fixture.Work.ShouldBeNull();
        ReportProductionCost("tail", head, completed, pageSize, 256, cost);
    }

    private static void ReportProductionCost(string mode, int head, int completed, int pageSize, int receiptLimit, ProjectionDeliveryCostScope cost, int routeCount = 1)
        => TestContext.Current.TestOutputHelper?.WriteLine("ProjectionCost " + JsonSerializer.Serialize(new
        {
            mode, head, completed, pageSize, receiptLimit, routeCount,
            cost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => cost[category]),
        }));
}
