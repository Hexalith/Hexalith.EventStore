using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ContentFreeRebuild_ActualGuardReplayFinalizationRecoveryAndCurrentInspectionPreserveTheOriginalCompletion(bool interrupt, bool mixed)
    {
        using var fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, 2, handlerSemantics: "full-replay-v1");
        for (int index = 0; index < fixture.Handlers.Count; index++)
            fixture.Handlers[index].ContentFreeRebuildCompletion = !mixed || index == 0;
        await fixture.UseProductionOwnersAsync();
        string[] routes = fixture.Catalog.OrdinaryProjectionTypes.ToArray();
        if (interrupt) fixture.InterruptBeforePendingRoute = routes[1];
        var delivered = await fixture.DeliverAsync(256);
        if (interrupt)
        {
            delivered.AlreadyCurrent.ShouldBeFalse();
            fixture.Work.ShouldNotBeNull();
            fixture.InterruptBeforePendingRoute = null;
            fixture.ReconstructGuardedDelivery();
            await fixture.RunGuardedRetryAsync();
        }
        else delivered.AlreadyCurrent.ShouldBeTrue();
        fixture.Work.ShouldBeNull();
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 3 && row.ActiveReservation == null
            && row.IdleBindingPending == null);
        foreach (string route in routes)
            fixture.Rows[route].GuardEvidence!.OwnerProof.Selection.ReceiptProjectionName.ShouldBe(
                fixture.Handlers.Single(handler => handler.ProjectionType == route).ContentFreeRebuildCompletion
                    ? ReadModelRebuildCompletion.ReceiptProjectionName : "rebuild");
        var original = SourceProofCodec.ToNative(fixture.Head);
        foreach (string route in routes)
        {
            var model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value!;
            model.NativeSourceProof.ShouldBe(original);
            fixture.Handlers.Single(handler => handler.ProjectionType == route).RebuildCalls.ShouldBe(1);
        }
        int writes = fixture.Backend.TrySaveByteOperations.Count, rowWrites = fixture.RowWrites;
        int forbidden = 0;
        fixture.Backend.BeforeReadByteState = key =>
        {
            if (!mixed && key.StartsWith("hxrmb:v1:marker:", StringComparison.Ordinal)
                || key.StartsWith("projection:rebuild", StringComparison.Ordinal)
                || key.EndsWith("-index", StringComparison.Ordinal))
            { forbidden++; throw new Xunit.Sdk.XunitException("Current inspection attempted a rich read: " + key); }
        };
        fixture.Events.Length.ShouldBe(3);
        fixture.ReturnedSequences.Clear();
        var inspected = await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None);
        inspected.AlreadyCurrent.ShouldBeTrue();
        inspected.ProvenSource.ShouldNotBeNull();
        SourceProofCodec.ToNative(inspected.ProvenSource).ShouldBe(original);
        inspected.CatalogFingerprint.ShouldBe(fixture.Catalog.CatalogFingerprint);
        inspected.ProvenOwners.Count.ShouldBe(routes.Length);
        foreach (ProjectionOwnerProof proof in inspected.ProvenOwners.Values)
        {
            proof.ShouldBe(fixture.Rows[proof.Selection.ProjectionName].GuardEvidence!.OwnerProof);
            proof.Selection.ReceiptProjectionName.ShouldBe(fixture.Handlers.Single(handler =>
                handler.ProjectionType == proof.Selection.ProjectionName).ContentFreeRebuildCompletion
                    ? ReadModelRebuildCompletion.ReceiptProjectionName : "rebuild");
        }
        fixture.ReturnedSequences.ShouldBeEmpty();
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
        fixture.RowWrites.ShouldBe(rowWrites);
        forbidden.ShouldBe(0);
    }
    [Fact]
    public async Task ContentFreeRebuild_ValidDigestFingerprintReplacementCannotBypassTheProductionCurrentBinding()
    {
        using var fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, handlerSemantics: "full-replay-v1");
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string earlierFingerprint = fixture.Rows[route].GuardEvidence!.OwnerProof.ReceiptFingerprint;
        fixture.Handlers.Single().ContentFreeRebuildCompletion = true;
        await fixture.UseProductionOwnersAsync();
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        var currentOwner = fixture.Rows[route].GuardEvidence!.OwnerProof;
        currentOwner.ReceiptFingerprint.ShouldNotBe(earlierFingerprint);
        var scope = new ReadModelBatchScope("models", fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, "rebuild", currentOwner.Selection.BatchId);
        string key = ReadModelBatchKeys.CompletionKey(scope.ComputeScopeHash());
        var receipt = ReadModelBatchCompletionProof.Read(fixture.Backend.ByteStoreValue(key)).ShouldNotBeNull().ToReceipt();
        receipt.Fingerprint = earlierFingerprint;
        byte[] replacement = JsonSerializer.SerializeToUtf8Bytes(ReadModelBatchCompletionProof.From(receipt), JsonSerializerOptions.Web);
        ReadModelBatchCompletionProof.Read(replacement).ShouldNotBeNull();
        fixture.Backend.SeedByteStore(key, replacement);
        // The low-level reader has no invented expected fingerprint. Actual source-guard currentness
        // compares this validly encoded owner evidence with its original persisted binding.
        var lowLevel = await fixture.Models.ReadOwnerProofAsync(new(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, route, fixture.Catalog.CatalogFingerprint), new("models", route));
        lowLevel.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(earlierFingerprint);
        int writes = fixture.Backend.TrySaveByteOperations.Count, rowWrites = fixture.RowWrites;
        fixture.Backend.BeforeReadByteState = read => read.StartsWith("hxrmb:v1:marker:", StringComparison.Ordinal).ShouldBeFalse();
        fixture.ReturnedSequences.Clear();
        var refused = await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None);
        refused.AlreadyCurrent.ShouldBeFalse();
        refused.ProvenOwners.ShouldBeEmpty();
        fixture.ReturnedSequences.ShouldBeEmpty();
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
        fixture.RowWrites.ShouldBe(rowWrites);
        fixture.Backend.ByteStoreValue(key).ShouldBe(replacement);
    }

}
