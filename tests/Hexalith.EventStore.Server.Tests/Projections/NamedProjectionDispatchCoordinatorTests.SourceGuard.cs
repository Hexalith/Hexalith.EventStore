using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedCanonicalSiblings_InterruptedSecondPendingWriteResumesCompleteOriginalInventory(bool mixedReplay)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, 2,
            handlerSemantics: mixedReplay ? "mapped-incremental-replay-v1" : "full-replay-v1", mixedReplay: mixedReplay);
        if (mixedReplay)
        {
            string mapped = fixture.Catalog.MappedOnlyStreamedProjectionTypes.Single();
            foreach (string key in new[] { fixture.Identity.ActorId + ":" + mapped, fixture.Identity.ActorId + ":" + mapped + "-index" })
            {
                ReadModelEntry<GuardedTailModel> prior = await fixture.Models.GetAsync<GuardedTailModel>("models", key);
                (await fixture.Models.TryEraseAsync("models", key, prior.ETag!)).ShouldBeTrue();
            }
        }
        await fixture.UseProductionOwnersAsync();
        string[] routes = fixture.Catalog.OrdinaryProjectionTypes.ToArray();
        fixture.InterruptBeforePendingRoute = routes[1];
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Rows[routes[0]].LastDeliveredSequence.ShouldBe(3);
        fixture.Rows[routes[1]].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[routes[1]].ActiveReservation.ShouldNotBeNull();
        fixture.Rows[routes[1]].IdleBindingPending.ShouldBeNull();
        fixture.Work.ShouldNotBeNull().PendingRoutes.ShouldBe(routes);
        string[] keys = fixture.Http.ReplayPreparationKeys.ToArray();
        keys.Length.ShouldBe(2);
        keys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key));
        fixture.Handlers.ShouldAllBe(handler => handler.RebuildCalls == 1);
        string dispatch = fixture.Work.DispatchId;
        fixture.InterruptBeforePendingRoute = null;
        fixture.ReconstructGuardedDelivery();
        await fixture.RunGuardedRetryAsync();
        fixture.Work.ShouldBeNull();
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 3 && row.ActiveReservation == null && row.IdleBindingPending == null
            && row.GuardEvidence!.OwnerProof.Selection.BatchId == dispatch);
        fixture.Handlers.ShouldAllBe(handler => handler.RebuildCalls == 1);
        keys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key) == key.EndsWith(":-3", StringComparison.Ordinal));
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None)).AlreadyCurrent.ShouldBeTrue();
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
    }

    [Theory]
    [InlineData("single-event-v1")]
    [InlineData("full-replay-v1")]
    public async Task GuardedReservedCleanup_SchedulerFailureHoldsNewerHeadUntilExactOriginalReclamation(string semantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, handlerSemantics: semantics);
        await fixture.UseProductionOwnersAsync();
        if (semantics == "full-replay-v1") fixture.Http.LoseReplayResponseAfterAction = "commit";
        else fixture.Http.LoseResponseAfterSequence = 2;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Work.ShouldNotBeNull().ReservationFencingTokens.ShouldNotBeEmpty();
        fixture.RejectSchedulerDelete = true;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Work.ShouldNotBeNull().HeadSequence.ShouldBe(3);
        fixture.Work.ReservationFencingTokens.ShouldNotBeEmpty();
        int preparationCount = fixture.Handlers.Single().RebuildCalls;
        await fixture.AppendPersistedSourceEventAsync();
        fixture.ReconstructGuardedDelivery();
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            (await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None)).AlreadyCurrent.ShouldBeFalse();
            cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        }
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        fixture.Work.HeadSequence.ShouldBe(3);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(preparationCount);
        fixture.RejectSchedulerDelete = false;
        await fixture.RunGuardedRetryAsync();
        fixture.Work.ShouldBeNull();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(preparationCount);
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(4);
    }

    [Fact]
    public async Task GuardedDelivery_CallerCancellationPropagatesBeforeSourceOrHandlerWork()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1);
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.Probe.TryDeliverAsync(fixture.Identity, 1, caller.Token));
        cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        fixture.Work.ShouldBeNull();
    }

    [Fact]
    public async Task ScheduledCompletedReplaySubset_CleanupFailureRetainsItsOriginalMaterialBeforeExactPruning()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1, 2,
            handlerSemantics: "full-replay-v1", unguardedLastRoute: true).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        fixture.Http.RejectReplayCleanup = true;
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        fixture.Rows[routes[0]].LastDeliveredSequence.ShouldBe(3);
        fixture.Rows[routes[0]].GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.ReceiptProjectionName.ShouldBe("rebuild");
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        string[] retainedKeys = fixture.Http.ReplayPreparationKeys.ToArray();
        retainedKeys.Length.ShouldBe(2);
        retainedKeys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key));
        int preparations = fixture.Handlers[0].RebuildCalls, calls = fixture.Handlers.Sum(static handler => handler.Calls);
        await fixture.RunGuardedRetryAsync(original).ConfigureAwait(true);
        fixture.Work.ShouldNotBeNull().PendingRoutes.ShouldBe(original.PendingRoutes);
        fixture.Work.Attempt.ShouldBe(original.Attempt + 1);
        retainedKeys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key));
        fixture.Http.RejectReplayCleanup = false;
        await fixture.RunGuardedRetryAsync().ConfigureAwait(true);
        fixture.Work.ShouldNotBeNull().PendingRoutes.ShouldBe([routes[1]]);
        retainedKeys.ShouldAllBe(key => fixture.Backend.ByteStoreContains(key) == key.EndsWith(":-3", StringComparison.Ordinal));
        fixture.Handlers[0].RebuildCalls.ShouldBe(preparations);
        fixture.Handlers.Sum(static handler => handler.Calls).ShouldBe(calls);
        (await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task GuardedCurrent_MalformedCompletedRetentionFloorCannotBeNormalizedIntoProof(int floor)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState original = fixture.Rows[route];
        fixture.Rows[route] = original with { GuardEvidence = original.GuardEvidence! with
            { Source = original.GuardEvidence.Source with { RetainedFloor = floor } } };
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        fixture.RowWrites.ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
    }

    [Theory]
    [InlineData(3, 4, true, 0)]
    [InlineData(1, 2, true, 6)]
    [InlineData(1, 3, false, 0)]
    public async Task GuardedRetentionAdvance_PreservesCompletedPrefixAndRequiresEveryUnreadTailEvent(int completed, int floor, bool current, int envelopeReads)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, completed).ConfigureAwait(true);
        fixture.AdvanceRetainedFloor(floor);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBe(current);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(envelopeReads);
        if (completed == 3 || !current)
        {
            cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            fixture.RowWrites.ShouldBe(0);
        }
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(current ? 3 : completed);
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task GuardedTail_RetentionMovesDuringReleaseCompletesOnlyWhileTheFrozenTailRemainsAvailable(int floor, bool completes)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.BeforeDeliveryReleaseAsync = () => fixture.AdvancePersistedRetainedFloorAsync(floor);
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse("a changed final source sample needs a later coherent Current inspection");
        ProjectionDeliveryState row = fixture.Rows.Values.Single();
        row.LastDeliveredSequence.ShouldBe(completes ? 3 : 1);
        if (completes)
        {
            row.IdleBindingPending.ShouldBeNull();
            row.ActiveReservation.ShouldBeNull();
            row.GuardEvidence.ShouldNotBeNull().Source.RetainedFloor.ShouldBe(1, "immutable admitted evidence is preserved");
            fixture.Handlers.Single().Calls.ShouldBe(2);
            (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            fixture.Handlers.Single().Calls.ShouldBe(2);
        }
        else
        {
            result.ReasonCode.ShouldBe("source_conflict");
            row.IdleBindingPending.ShouldNotBeNull();
            row.ActiveReservation.ShouldNotBeNull();
            fixture.Work.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GuardedTail_AppendAfterCompletionCasBeforeReadbackCompletesFrozenHeadWithoutClaimingCurrent()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.AfterCompletionSaveAsync = fixture.AppendPersistedSourceEventAsync;
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
            result.AlreadyCurrent.ShouldBeFalse();
            result.ReasonCode.ShouldBe("head_changed");
            cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(3);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(2);
        }
        ProjectionDeliveryState completed = fixture.Rows.Values.Single();
        completed.LastDeliveredSequence.ShouldBe(3);
        completed.GuardEvidence.ShouldNotBeNull().Source.HeadSequence.ShouldBe(3);
        completed.ActiveReservation.ShouldBeNull();
        completed.IdleBindingPending.ShouldBeNull();
        fixture.Head.HeadSequence.ShouldBe(4);
        fixture.Handlers.Single().Calls.ShouldBe(2);
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(4);
        fixture.Handlers.Single().Calls.ShouldBe(3);
    }

    [Fact]
    public async Task GuardedDescriptor_OrdinaryOperatorRebuildRetainsItsConfiguredWiderManifestWithoutActivatingProof()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3,
            handlerSemantics: "full-replay-v1", manifestLimit: 2_097_152).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync(activateGuard: false).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState original = fixture.Rows[route];
        fixture.Handlers.Single().RebuildPaddingBytes = 600_000; // two writes exceed the guarded 1 MiB profile
        fixture.Handlers.Single().UnconditionalReplay = true;
        await fixture.CompleteOperatorRebuildAsync("wide-generation-2").ConfigureAwait(true);
        GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
            .ConfigureAwait(true)).Value.ShouldNotBeNull();
        model.Count.ShouldBe(3);
        model.Padding!.Length.ShouldBe(600_000);
        model.ProjectionOwner.OwnerGeneration.ShouldBe("wide-generation-2");
        fixture.Rows[route].ShouldBe(original);
        (await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScheduledGuardedSubsetCleanup_PreservesUnguardedAndTerminalSiblingWork(bool mismatchedWork)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1, 2,
            unguardedLastRoute: true).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        var original = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, 1), fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, fixture.Catalog.AppId, fixture.Catalog.ServiceVersion, 1, fixture.Head.HeadMessageId!,
            routes, ["terminal-retained"], fixture.Head.HeadMessageId!, fixture.Catalog.CatalogFingerprint, 2, fixture.Clock.GetUtcNow(), null)
            { ReservationFencingTokens = routes.ToDictionary(static route => route, static _ => 7L, StringComparer.Ordinal) };
        if (mismatchedWork)
        {
            original = original with { HeadMessageId = "different-head", DispatchId = "different-head" };
        }
        await fixture.RunGuardedRetryAsync(original).ConfigureAwait(true);
        ProjectionDeliveryRetryWorkItem retained = fixture.Work.ShouldNotBeNull();
        retained.PendingRoutes.ShouldBe(mismatchedWork ? routes : [routes[1]]);
        retained.TerminalRoutes.ShouldBe(["terminal-retained"]);
        retained.ReservationFencingTokens.Keys.Order().ShouldBe(retained.PendingRoutes.Order());
        retained.Attempt.ShouldBe(3);
        retained.LeaseOwner.ShouldBeNull();
        fixture.RowWrites.ShouldBe(0);
        fixture.Handlers.ShouldAllBe(static handler => handler.Calls == 0 && handler.RebuildCalls == 0);
        if (!mismatchedWork) fixture.Ranges.ShouldBeEmpty();
        (await fixture.Probe.InspectCurrentAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
    }

    [Theory]
    [InlineData("owner", false)]
    [InlineData("applied", false)]
    [InlineData("reconcile", false)]
    [InlineData("owner", true)]
    [InlineData("applied", true)]
    [InlineData("reconcile", true)]
    public async Task SourceGuardProof_FreshReplicaRecomputesItsCompleteDescriptorInventoryBeforeReadingPersistedReceipts(string action, bool extraDomain)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3, extraDomain: extraDomain).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionOwnerProofResult result = await fixture.ReadOnFreshReplicaAsync(action, route).ConfigureAwait(true);
        result.Proof.ShouldNotBeNull().ShouldBe(fixture.Rows[route].GuardEvidence!.OwnerProof);
        result.ReasonCode.ShouldBeNull();
        ProjectionOwnerProofResult allDomains = await fixture.ReadOnFreshReplicaAsync(action, route,
            fixture.AllDomainsFingerprint()).ConfigureAwait(true);
        allDomains.Proof.ShouldBe(result.Proof);
        ProjectionOwnerProofResult changed = await fixture.ReadOnFreshReplicaAsync(action, route, "changed-catalog").ConfigureAwait(true);
        changed.Proof.ShouldBeNull();
        changed.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(0);
        fixture.RowWrites.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedScope_CorruptSiblingEvidenceStaysUntouchedWhileLaterQualifiedSiblingCompletes(bool corruptMarker)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1, 2).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        string corruptKey = corruptMarker ? ProjectionDeliveryStateKeys.GetSourceGuardKey(fixture.Identity, routes[0])
            : ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, routes[0]);
        byte[] corrupted = corruptMarker ? "{\"schemaVersion\":999}"u8.ToArray() : "{"u8.ToArray();
        fixture.Backend.SeedByteStore(corruptKey, corrupted);
        ProjectionDeliveryState sibling = fixture.Rows[routes[0]];

        ProjectionSourceGuardDeliveryResult result = await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true);

        result.Owned.ShouldBeTrue();
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("guard_activation_required");
        fixture.Rows[routes[1]].LastDeliveredSequence.ShouldBe(2);
        fixture.Rows[routes[0]].ShouldBe(sibling);
        fixture.Backend.ByteStoreValue(corruptKey).ShouldBe(corrupted);
        fixture.Handlers[0].Calls.ShouldBe(0);
        fixture.Handlers[1].Calls.ShouldBe(1);
        fixture.RowWrites.ShouldBe(3);
    }

    [Fact]
    public async Task GuardedScope_UnguardedSiblingCannotDisableItsAdmissionOrManufactureCatalogCurrent()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1, 2,
            unguardedLastRoute: true).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];

        ProjectionSourceGuardDeliveryResult result = await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true);

        result.Owned.ShouldBeTrue();
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("guard_activation_required");
        fixture.Rows[routes[0]].LastDeliveredSequence.ShouldBe(2);
        fixture.Rows[routes[0]].GuardEvidence.ShouldNotBeNull().Source.ShouldBe(fixture.Head);
        fixture.Rows[routes[1]].LastDeliveredSequence.ShouldBe(2);
        fixture.Rows[routes[1]].SchemaVersion.ShouldBe(1);
        fixture.Rows[routes[1]].GuardEvidence.ShouldBeNull();
        fixture.Handlers[0].Calls.ShouldBe(1);
        fixture.Handlers[1].Calls.ShouldBe(1);
        fixture.RowWrites.ShouldBe(5);
        fixture.Work.ShouldBeNull();
    }

    [Fact]
    public async Task GuardedDowngradedDetailRow_FailsClosedWhileIndependentlyQualifiedIndexSiblingCompletes()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1, 2).ConfigureAwait(true);
        string detail = fixture.Catalog.OrdinaryProjectionTypes[0], index = fixture.Catalog.OrdinaryProjectionTypes[1];
        ProjectionDeliveryState downgraded = fixture.Rows[detail] with { SchemaVersion = 1, GuardEvidenceVersion = null, GuardEvidence = null };
        fixture.Rows[detail] = downgraded;
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("delivery_schema_regression");
        fixture.Rows[detail].ShouldBe(downgraded);
        fixture.Handlers.Single(handler => handler.ProjectionType == detail).Calls.ShouldBe(0);
        fixture.Handlers.Single(handler => handler.ProjectionType == index).Calls.ShouldBe(1);
        fixture.Rows[index].LastDeliveredSequence.ShouldBe(2);
        fixture.Rows[index].GuardEvidence.ShouldNotBeNull().Source.ShouldBe(fixture.Head);
        fixture.Rows[index].ActiveReservation.ShouldBeNull();
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + detail).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(1);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + index).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuardedCanonicalFallback_PrivateBatchManifestCleanupRemainsOwnedUntilSameWorkRetry(bool throws)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1, "full-replay-v1");
        fixture.Handlers.Single().RebuildPaddingBytes = 7_000;
        string key = ReadModelBatchKeys.ManifestKey(new ReadModelBatchScope("models", fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, "rebuild", fixture.Head.HeadMessageId!).ComputeScopeHash());
        fixture.Backend.TryDeleteByteOverride = target => target == key ? throws ? throw new IOException("Cleanup unavailable.") : false : null;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        fixture.Backend.ByteStoreContains(key).ShouldBeTrue();
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string modelKey = fixture.Identity.ActorId + ":" + route;
        byte[] detailBefore = fixture.Backend.ByteStoreValue(modelKey), indexBefore = fixture.Backend.ByteStoreValue(modelKey + "-index");
        fixture.Backend.TryDeleteByteOverride = null!;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Work.ShouldBeNull();
        fixture.Backend.ByteStoreContains(key).ShouldBeFalse();
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(257);
        fixture.Rows.Values.Single().GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.BatchId.ShouldBe(original.DispatchId);
        fixture.Backend.ByteStoreValue(modelKey).ShouldBe(detailBefore);
        fixture.Backend.ByteStoreValue(modelKey + "-index").ShouldBe(indexBefore);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        cost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(1, "the same staging recovery attempt is charged");
        int deletes = fixture.Backend.TryDeleteByteOperations.Count, writes = fixture.RowWrites;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Backend.TryDeleteByteOperations.Count.ShouldBe(deletes);
        fixture.RowWrites.ShouldBe(writes);
    }

    [Fact]
    public async Task GuardedCanonicalFallback_CompletedRetryReclaimsTheSeparateOriginalBatchManifestWithoutRepreparing()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1, "full-replay-v1");
        fixture.Handlers.Single().RebuildPaddingBytes = 7_000;
        string batchManifestKey = ReadModelBatchKeys.ManifestKey(new ReadModelBatchScope("models", fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, "rebuild", fixture.Head.HeadMessageId!).ComputeScopeHash());
        byte[]? originalManifest = null;
        fixture.Backend.BeforeTrySaveByteState = (key, value) => { if (key == batchManifestKey) originalManifest = value.ToArray(); };
        fixture.Http.RejectReplayCleanup = true;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        ProjectionDeliveryRetryWorkItem work = fixture.Work.ShouldNotBeNull();
        originalManifest.ShouldNotBeNull();
        // Retained terminal work from an earlier writer can outlive its private
        // cleanup response. Recreate those exact immutable original bytes.
        fixture.Backend.SeedByteStore(batchManifestKey, originalManifest!);
        int writes = fixture.RowWrites, deletes = fixture.Backend.TryDeleteByteOperations.Count;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Backend.TryDeleteByteOperations.Count.ShouldBe(deletes);
        fixture.Backend.ByteStoreContains(batchManifestKey).ShouldBeTrue();
        fixture.Http.RejectReplayCleanup = false;
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        await fixture.RunGuardedRetryAsync(work);
        fixture.Work.ShouldBeNull();
        fixture.Backend.ByteStoreContains(batchManifestKey).ShouldBeFalse();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(257);
        fixture.RowWrites.ShouldBe(writes);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        costs[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        costs[ProjectionDeliveryCost.BatchExecutions].ShouldBe(0);
    }

    [Fact]
    public async Task CanonicalCleanupFailure_PreservesOriginalRetryMaterialAndCurrentProbeStaysReadOnly()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1,
            "full-replay-v1").ConfigureAwait(true);
        fixture.Http.RejectReplayCleanup = true;
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        original.DispatchId.ShouldBe(fixture.Head.HeadMessageId);
        int writes = fixture.RowWrites;
        using (ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            (await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
            cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
        }
        fixture.Work.ShouldBe(original);
        fixture.Http.RejectReplayCleanup = false;
        (await fixture.Probe.ReclaimCompletedReplayAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        (await fixture.Probe.ReclaimCompletedReplayAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        fixture.RowWrites.ShouldBe(writes);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(257);
    }

    [Fact]
    public async Task SingleEventExplicitRebuildRequiredBeforeEffects_SwitchesOnceToCanonicalUnderSameHeadAndFence()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        fixture.Handlers.Single().RequireRebuildBeforeEffects = true;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.Single().Calls.ShouldBe(1);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Dispatches.ShouldBe([fixture.Head.HeadMessageId!]);
        ProjectionDeliveryState row = fixture.Rows.Values.Single();
        row.LastDeliveredSequence.ShouldBe(257);
        row.GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.BatchId.ShouldBe(fixture.Head.HeadMessageId);
        row.GuardEvidence.OwnerProof.Selection.ReceiptProjectionName.ShouldBe("rebuild");
        row.CompletedMessageIdDigests!.Count.ShouldBe(257);
        fixture.RowWrites.ShouldBe(4); // reserve + explicit fenced mode switch + pending + completion
        fixture.Work.ShouldBeNull();
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBeLessThanOrEqualTo((3 * 256) + (3 * 257));
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
        foreach (string suffix in new[] { "route-1", "route-1-index" })
        {
            string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
            string key = fixture.Identity.ActorId + ":" + (suffix.EndsWith("-index", StringComparison.Ordinal) ? route + "-index" : route);
            (await fixture.Models.GetAsync<GuardedTailModel>("models", key).ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(257);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MixedSharedRoute_UnqualifiedOwnerProofPreventsWholeCatalogCurrentOrPartialEffects(int head)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, 1, 2,
            sharedLastRoute: true).ConfigureAwait(true);
        ProjectionSourceGuardDeliveryResult result = await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true);
        result.Owned.ShouldBeTrue();
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBeOneOf(ProjectionDispatchReasonCodes.UnsupportedCapability, "delivery_rebuild_required");
        fixture.RowWrites.ShouldBe(0);
        fixture.Work.ShouldBeNull();
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 0 && handler.RebuildCalls == 0);
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
                .ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("owner")]
    [InlineData("message")]
    [InlineData("unconditional")]
    public async Task GuardedCanonicalFallback_RejectsChangedHeadGenerationOrUnfencedPlanBeforePublication(string invalid)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1,
            "full-replay-v1").ConfigureAwait(true);
        GuardedTailProjectionHandler handler = fixture.Handlers.Single();
        handler.ReplayGenerationOverride = invalid == "generation" ? "foreign-generation" : null;
        handler.ReplayOwnerIdentityOverride = invalid == "owner" ? "foreign-owner" : null;
        handler.ReplayMessageIdOverride = invalid == "message" ? "foreign-message" : null;
        handler.UnconditionalReplay = invalid == "unconditional";
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull();
        fixture.Work.ShouldNotBeNull();
        handler.RebuildCalls.ShouldBe(1);
        handler.Calls.ShouldBe(0);
        foreach (string suffix in new[] { route, route + "-index" })
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + suffix)
                .ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Fact]
    public async Task CanonicalAdmission_SharesOnlyOneCompleteReadablePrefixAndVerificationRetainsNoSecondPrefix()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 3,
            "full-replay-v1").ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        ProjectionSourceGuardReplayAdmission first = await fixture.AdmitCanonicalRouteAsync(routes[0]).ConfigureAwait(true);
        foreach (string route in routes.Skip(1))
        {
            ProjectionSourceGuardReplayAdmission next = await fixture.AdmitCanonicalRouteAsync(route, first.Events).ConfigureAwait(true);
            ReferenceEquals(first.Events, next.Events).ShouldBeTrue();
            next.Tail.Events.Count.ShouldBe(256);
            next.Tail.History.PrefixFingerprint.ShouldNotBe(first.Tail.History.PrefixFingerprint,
                "each independently authenticated route retains its own scoped prefix proof");
        }
        ProjectionSourceGuardReplayAdmission verification = await fixture.AdmitCanonicalRouteAsync(routes[0],
            retainEvents: false).ConfigureAwait(true);
        verification.Events.ShouldBeEmpty();
        verification.Tail.Events.ShouldBe(first.Tail.Events);
        fixture.ReturnedSequences.Count.ShouldBe(4 * 257);
        ProjectionEventDto[] changed = first.Events.ToArray();
        changed[256] = changed[256] with { Payload = "changed"u8.ToArray() };
        ProjectionDeliveryHistoryValidationException conflict = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(
            () => fixture.AdmitCanonicalRouteAsync(routes[1], changed)).ConfigureAwait(true);
        conflict.Message.ShouldBe("source_conflict");
    }

    [Fact]
    public async Task CanonicalAdmission_SharedCompressedProtectedPrefixKeepsOneReadableEventBesideTheOriginalPrefix()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(48, 1, 2,
            "full-replay-v1").ConfigureAwait(true);
        fixture.SetProtectedTailPayloadSizes(1, Enumerable.Repeat(1_048_576, 47).ToArray());
        string[] routes = [.. fixture.Catalog.OrdinaryProjectionTypes];
        ProjectionSourceGuardReplayAdmission original = await fixture.AdmitCanonicalRouteAsync(routes[0]).ConfigureAwait(true);
        original.Events.Sum(static item => (long)item.Payload.Length).ShouldBe(47L * 1_048_576 + 2);
        original.Tail.Pages.ShouldBe(1);
        ProjectionSourceGuardReplayAdmission shared = await fixture.AdmitCanonicalRouteAsync(routes[1], original.Events).ConfigureAwait(true);
        shared.Events.ShouldBeSameAs(original.Events);
        shared.Tail.Pages.ShouldBe(1);
        shared.Tail.SourceBytes.ShouldBeLessThan(2_097_152);
        shared.Tail.WireBytes.ShouldBe(original.Tail.WireBytes);
        shared.Tail.WireBytes.ShouldBeLessThan(67_108_864);
        fixture.ProtectedReads.ShouldBe(94);
        fixture.Ranges.Count.ShouldBe(2);
        fixture.Handlers.ShouldAllBe(static handler => handler.RebuildCalls == 0 && handler.Calls == 0);
    }

    [Fact]
    public async Task GuardedCanonicalFallback_ThreeFullReplayRoutesPublishOneCompleteCoordinatedReplacement()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 3,
            "full-replay-v1").ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.ShouldAllBe(handler => handler.RebuildCalls == 1 && handler.Calls == 0);
        fixture.Rows.Values.ShouldAllBe(row => row.LastDeliveredSequence == 257 && row.ActiveReservation == null
            && row.IdleBindingPending == null && row.CompletedMessageIdDigests!.Count == 257);
        fixture.Rows.Values.Select(row => row.GuardEvidence!.OwnerProof.ReceiptIdentity).Distinct().Count().ShouldBe(1);
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            GuardedTailModel detail = (await fixture.Models.GetAsync<GuardedTailModel>("models",
                fixture.Identity.ActorId + ":" + route).ConfigureAwait(true)).Value.ShouldNotBeNull();
            detail.Count.ShouldBe(257);
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")
                .ConfigureAwait(true)).Value.ShouldBe(detail);
        }
        fixture.Work.ShouldBeNull();
    }

    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(771)]
    [InlineData(10000)]
    public async Task GuardedCanonicalFallback_FullReplayReceivesOneCompletePrefixAndFinalizesOnce(int head)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, 1, 1,
            "full-replay-v1").ConfigureAwait(true);
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(256).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeTrue();
        costs[ProjectionDeliveryCost.BatchExecutions].ShouldBe(1);
        costs[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.ReturnedSequences.Count.ShouldBe(2 * head);
        fixture.Ranges.Count.ShouldBe(2 * ((head + 255) / 256));
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState row = fixture.Rows[route];
        row.LastDeliveredSequence.ShouldBe(head);
        row.ActiveReservation.ShouldBeNull();
        row.IdleBindingPending.ShouldBeNull();
        row.CompletedMessageIdDigests!.Count.ShouldBe(head);
        row.GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.ReceiptProjectionName.ShouldBe("rebuild");
        GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
            .ConfigureAwait(true)).Value.ShouldNotBeNull();
        model.Count.ShouldBe(head);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")
            .ConfigureAwait(true)).Value.ShouldBe(model);
        fixture.RowWrites.ShouldBe(3);
        fixture.Work.ShouldBeNull();
        fixture.ReturnedSequences.Clear();
        fixture.Ranges.Clear();
        (await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.ReturnedSequences.ShouldBeEmpty();
        fixture.Ranges.ShouldBeEmpty();
        fixture.RowWrites.ShouldBe(3);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("commit")]
    public async Task GuardedCanonicalFallback_MixedStreamedAndFullRoutesResumeOneOriginalCoordinatedManifest(string lostAction)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, routeCount: 2,
            handlerSemantics: "mapped-incremental-replay-v1", mixedReplay: true);
        string mapped = fixture.Catalog.MappedOnlyStreamedProjectionTypes.ShouldHaveSingleItem();
        foreach (string key in new[] { fixture.Identity.ActorId + ":" + mapped, fixture.Identity.ActorId + ":" + mapped + "-index" })
        {
            ReadModelEntry<GuardedTailModel> prior = await fixture.Models.GetAsync<GuardedTailModel>("models", key);
            (await fixture.Models.TryEraseAsync("models", key, prior.ETag!)).ShouldBeTrue();
        }
        fixture.Http.LoseReplayResponseAfterAction = lostAction;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeFalse();
        fixture.Work.ShouldNotBeNull().PendingRoutes.Count.ShouldBe(2);
        fixture.Rows.Values.ShouldAllBe(static row => row.LastDeliveredSequence == 1 && row.ActiveReservation != null);
        string dispatch = fixture.Work!.DispatchId;
        fixture.Http.ReplayPreparationKeys.Count.ShouldBe(2);
        fixture.Handlers.ShouldAllBe(static handler => handler.RebuildCalls == 1 && handler.Calls == 0);
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Work.ShouldBeNull();
        fixture.Http.ReplayPreparationKeys.Count.ShouldBe(2);
        fixture.Handlers.ShouldAllBe(static handler => handler.RebuildCalls == 1 && handler.Calls == 0);
        fixture.Http.ReplayPartSizes.ShouldNotBeEmpty();
        fixture.Http.ReplayPartSizes.ShouldAllBe(static size => size > 0 && size <= ProjectionRebuildTransportReference.PartBytes);
        string? fingerprint = null;
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            ProjectionDeliveryState row = fixture.Rows[route];
            row.LastDeliveredSequence.ShouldBe(257);
            ProjectionOwnerProof proof = row.GuardEvidence.ShouldNotBeNull().OwnerProof;
            proof.Selection.BatchId.ShouldBe(dispatch);
            proof.Selection.ReceiptProjectionName.ShouldBe("rebuild");
            fingerprint ??= proof.ReceiptFingerprint;
            proof.ReceiptFingerprint.ShouldBe(fingerprint);
            GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull();
            model.Count.ShouldBe(257);
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")).Value.ShouldBe(model);
        }
        int writes = fixture.Backend.TrySaveByteOperations.Count;
        (await fixture.DeliverAsync(256)).AlreadyCurrent.ShouldBeTrue();
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(writes);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("commit")]
    public async Task GuardedCanonicalFallback_LostResponseResumesOriginalManifestWithoutPreparingAgain(string action)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1,
            "full-replay-v1").ConfigureAwait(true);
        fixture.Http.LoseReplayResponseAfterAction = action;
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        ProjectionDeliveryReservation original = fixture.Rows.Values.Single().ActiveReservation.ShouldNotBeNull();
        original.GuardBinding.ShouldNotBeNull().CanonicalReplay.ShouldBeTrue();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        fixture.Work.ShouldNotBeNull().DispatchId.ShouldBe(original.DispatchId);
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(257);
        fixture.Work.ShouldBeNull();
    }

    [Theory]
    [InlineData(257)]
    [InlineData(10000)]
    public async Task GuardedCanonicalFallback_ColdMappedOwnerUsesCompletePrefixUnderOriginalAdmittedGeneration(int head)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, 1, 1,
            "mapped-incremental-replay-v1").ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string key = fixture.Identity.ActorId + ":" + route;
        foreach (string modelKey in new[] { key, key + "-index" })
        {
            ReadModelEntry<GuardedTailModel> current = await fixture.Models.GetAsync<GuardedTailModel>("models", modelKey).ConfigureAwait(true);
            (await fixture.Models.TryEraseAsync("models", modelKey, current.ETag!).ConfigureAwait(true)).ShouldBeTrue();
        }
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DeliverAsync(256).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.ReturnedSequences.Count.ShouldBe(2 * head);
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(head);
        fixture.Http.ReplayPartSizes.ShouldNotBeEmpty();
        fixture.Http.ReplayPartSizes.ShouldAllBe(size => size > 0 && size <= ProjectionRebuildTransportReference.PartBytes);
        cost[ProjectionDeliveryCost.ReplayPartUploadAttempts].ShouldBe(fixture.Http.ReplayPartSizes.Count);
        cost[ProjectionDeliveryCost.ReplayPartUploadBytes].ShouldBe(fixture.Http.ReplayPartSizes.Sum(static size => (long)size));
        cost[ProjectionDeliveryCost.ReplayTransportSerializationBytes].ShouldBe(2 * cost[ProjectionDeliveryCost.ReplayPartUploadBytes]);
        if (head == 10000) fixture.Http.ReplayPartSizes.Count.ShouldBeGreaterThan(1);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", key).ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(head);
    }

    [Fact]
    public async Task GuardedCanonicalReplay_StageCommitVerifyRestoreOriginalPreparationAcrossRestart()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 2,
            "mapped-incremental-replay-v1").ConfigureAwait(true);
        foreach (string action in new[] { "stage", "stage", "commit", "verify", "commit", "verify" })
            (await fixture.StagedCanonicalReplayAsync(action).ConfigureAwait(true)).Outcomes
                .ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        fixture.Handlers.ShouldAllBe(handler => handler.RebuildCalls == 1 && handler.Calls == 0);
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
                .ConfigureAwait(true)).Value.ShouldNotBeNull();
            (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")
                .ConfigureAwait(true)).Value.ShouldBe(model);
            model.Count.ShouldBe(257);
            model.ProjectionOwner.MessageId.ShouldBe(fixture.Head.HeadMessageId);
            (await fixture.Models.ReadOwnerProofAsync(new(fixture.Identity.TenantId, fixture.Identity.Domain,
                fixture.Identity.AggregateId, route, fixture.Catalog.CatalogFingerprint), new("models", route))
                .ConfigureAwait(true)).Proof.ShouldNotBeNull().Selection.ShouldBe(model.ProjectionOwner);
            fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GuardedCanonicalReplay_ChangedRequestCannotPrepareOrPublishAnotherManifestUnderSameIdentity(bool changePrivateGeneration,
        bool compactTransport)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 1,
            "mapped-incremental-replay-v1").ConfigureAwait(true);
        (await fixture.StagedCanonicalReplayAsync("stage", compactTransport: compactTransport).ConfigureAwait(true))
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var exception = await Should.ThrowAsync<Hexalith.EventStore.DomainService.ProjectionDispatchValidationException>(
            () => fixture.StagedCanonicalReplayAsync("commit", changedInput: !changePrivateGeneration,
                ownerGeneration: changePrivateGeneration ? "changed-generation" : "generation-1", compactTransport: compactTransport)).ConfigureAwait(true);
        exception.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(1);
        (await fixture.StagedCanonicalReplayAsync("abort", compactTransport: compactTransport).ConfigureAwait(true))
            .Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(257)]
    public async Task GuardedNewOwner_ExplicitEmptyScopeInitializesFromTrustedSeedAndFinalizesOnce(int head)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(0, 0).ConfigureAwait(true);
        ProjectionSourceGuardDeliveryResult empty = await fixture.DeliverAsync(1).ConfigureAwait(true);
        empty.AlreadyCurrent.ShouldBeFalse();
        empty.ReasonCode.ShouldBe("empty_no_work");
        fixture.RowWrites.ShouldBe(0);
        fixture.ReturnedSequences.ShouldBeEmpty();
        for (int index = 0; index < head; index++) fixture.AppendSourceEvent();
        ProjectionSourceGuardDeliveryResult applied = await fixture.DeliverAsync(256).ConfigureAwait(true);
        applied.AlreadyCurrent.ShouldBeTrue();
        fixture.RowWrites.ShouldBe(3);
        fixture.ReturnedSequences.Count.ShouldBe(3 * head);
        fixture.ReturnedSequences.ShouldAllBe(sequence => sequence > 0 && sequence <= head);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState row = fixture.Rows[route];
        row.LastDeliveredSequence.ShouldBe(head);
        row.GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.OwnerIdentity.ShouldBe(fixture.Identity.AggregateId);
        row.GuardEvidence.OwnerProof.Selection.OwnerGeneration.ShouldBe(fixture.Events[0].MessageId);
        row.CompletedMessageIdDigests!.Count.ShouldBe(head);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(head);
        fixture.Work.ShouldBeNull();
    }

    [Fact]
    public async Task GuardedNewOwner_LostFirstEventResponseRecoversOriginalGenerationAndSkipsItsEffect()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 0).ConfigureAwait(true);
        fixture.Http.LoseResponseAfterSequence = 1;
        (await fixture.DeliverAsync(1).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
        ProjectionDeliveryReservation original = fixture.Rows.Values.Single().ActiveReservation.ShouldNotBeNull();
        original.GuardBinding!.BaseOwnerProof.ShouldBeNull();
        original.GuardBinding.SelectedOwnerGeneration.ShouldBe(fixture.Events[0].MessageId);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(0);
        (await fixture.DeliverAsync(1).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.Single().Calls.ShouldBe(3);
        fixture.Handlers.Single().Dispatches.ShouldAllBe(id => id == original.DispatchId);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(3);
    }

    [Fact]
    public async Task CanonicalReplay_ProductionDispatcherBindsEveryRouteToOneExistingCoordinatedReceipt()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(257, 1, 2,
            "mapped-incremental-replay-v1").ConfigureAwait(true);
        ProjectionDispatchResponse response = await fixture.CanonicalReplayAsync().ConfigureAwait(true);
        response.Outcomes.Count.ShouldBe(2);
        response.Outcomes.ShouldAllBe(outcome => outcome.Status == ProjectionDispatchStatus.Completed);
        string? receipt = null;
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            ProjectionOwnerProofResult proof = await fixture.Models.ReadOwnerProofAsync(new(fixture.Identity.TenantId, fixture.Identity.Domain,
                fixture.Identity.AggregateId, route, fixture.Catalog.CatalogFingerprint), new("models", route)).ConfigureAwait(true);
            ProjectionOwnerSelection selection = proof.Proof.ShouldNotBeNull().Selection;
            selection.AppliedSequence.ShouldBe(257);
            selection.MessageId.ShouldBe(fixture.Head.HeadMessageId);
            selection.ReceiptProjectionName.ShouldBe("rebuild");
            receipt ??= proof.Proof.ReceiptIdentity;
            proof.Proof.ReceiptIdentity.ShouldBe(receipt);
            GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
                .ConfigureAwait(true)).Value.ShouldNotBeNull();
            GuardedTailModel index = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")
                .ConfigureAwait(true)).Value.ShouldNotBeNull();
            model.Count.ShouldBe(257);
            index.ShouldBe(model);
            fixture.Rows[route].LastDeliveredSequence.ShouldBe(1, "canonical rebuild must not advance delivery checkpoints");
        }
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 0 && handler.RebuildCalls == 1);
    }

    [Fact]
    public async Task GuardedTail_PartialManifestConflictRetainsOriginalWorkAndStartsNoLaterEvent()
    {
        using GuardedTailProductionHarness fixture = await CreatePartlyCompactedTailAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var scope = new ReadModelBatchScope("models", fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, route, fixture.Events[1].MessageId);
        (ReadOnlyMemory<byte> bytes, _) = await fixture.Backend.GetByteStateAndETagAsync("models", scope.ComputeMarkerKey(),
            null, null!, CancellationToken.None).ConfigureAwait(true);
        System.Text.Json.Nodes.JsonNode marker = System.Text.Json.Nodes.JsonNode.Parse(bytes.Span)!;
        byte[] manifest = Convert.FromBase64String(marker["manifest"]!.GetValue<string>());
        manifest[0] ^= 1;
        marker["manifest"] = Convert.ToBase64String(manifest);
        fixture.Backend.SeedByteStore(scope.ComputeMarkerKey(), JsonSerializer.SerializeToUtf8Bytes(marker));
        string dispatch = fixture.Rows[route].ActiveReservation!.DispatchId;

        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull().DispatchId.ShouldBe(dispatch);
        fixture.Work.ShouldNotBeNull().DispatchId.ShouldBe(dispatch);
        fixture.Handlers.Single().Calls.ShouldBe(1);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true))
            .Value.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GuardedTail_OriginalPartialManifestCannotOverwriteAReplacedOwnerGeneration()
    {
        using GuardedTailProductionHarness fixture = await CreatePartlyCompactedTailAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string key = fixture.Identity.ActorId + ":" + route;
        ReadModelEntry<GuardedTailModel> current = await fixture.Models.GetAsync<GuardedTailModel>("models", key).ConfigureAwait(true);
        GuardedTailModel replacement = current.Value! with { Count = 99,
            ProjectionOwner = current.Value!.ProjectionOwner with { OwnerGeneration = "replacement-generation", BatchId = "replacement-batch" } };
        var replacementBatch = new ReadModelBatch(new("models", fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, route, "replacement-batch"),
            [ReadModelBatchOperation.Write(key, replacement, ReadModelBatchConcurrency.Match(current.ETag!))])
        { OwnerSelection = replacement.ProjectionOwner };
        (await fixture.Models.ExecuteAsync(replacementBatch).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();

        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(1);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", key).ConfigureAwait(true)).Value.ShouldBe(replacement);
    }

    [Fact]
    public async Task GuardedTail_SourceGrowthDuringOriginalPartialRecoveryFinishesOnlyItsFrozenHead()
    {
        using GuardedTailProductionHarness fixture = await CreatePartlyCompactedTailAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string dispatch = fixture.Rows[route].ActiveReservation!.DispatchId;
        fixture.GrowOnInspectionNumber = fixture.SourceInspections + 1;
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1).ConfigureAwait(true);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("head_changed");
        fixture.Head.HeadSequence.ShouldBe(5);
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(4);
        fixture.Handlers.Single().Calls.ShouldBe(3);
        fixture.Handlers.Single().Dispatches.ShouldAllBe(item => item == dispatch);
        (await fixture.DeliverAsync(1).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(5);
        fixture.Handlers.Single().Calls.ShouldBe(4);
        fixture.Handlers.Single().Dispatches[^1].ShouldBe(fixture.Events[^1].MessageId);
    }

    private static async Task<GuardedTailProductionHarness> CreatePartlyCompactedTailAsync()
    {
        GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1).ConfigureAwait(false);
        string index = fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes.Single() + "-index";
        fixture.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == index && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) throw new IOException("Interrupted compaction.");
        };
        ProjectionSourceGuardDeliveryResult first = await fixture.DeliverAsync(256).ConfigureAwait(false);
        first.AlreadyCurrent.ShouldBeFalse();
        fixture.Backend.BeforeTrySaveByteState = null!;
        return fixture;
    }

    [Fact]
    public async Task GuardedTail_PartialCommittedEvent_ResumesOriginalManifestBeforeAnyLaterHandler()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string indexKey = fixture.Identity.ActorId + ":" + route + "-index";
        fixture.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == indexKey && !ReadModelBatchEnvelope.IsEnvelope(bytes.Span))
                throw new IOException("Interrupted original event compaction.");
        };
        ProjectionSourceGuardDeliveryResult interrupted = await fixture.DeliverAsync(256).ConfigureAwait(true);
        interrupted.AlreadyCurrent.ShouldBeFalse();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(1);
        fixture.Backend.BeforeTrySaveByteState = null!;
        var scope = new ReadModelBatchScope("models", fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, route, fixture.Events[1].MessageId);
        (ReadOnlyMemory<byte> originalBytes, _) = await fixture.Backend.GetByteStateAndETagAsync("models", scope.ComputeMarkerKey(),
            null, null!, CancellationToken.None).ConfigureAwait(true);
        using JsonDocument original = JsonDocument.Parse(originalBytes);
        string fingerprint = original.RootElement.GetProperty("fp").GetString()!;
        original.RootElement.GetProperty("manifest").GetBytesFromBase64().Length.ShouldBeGreaterThan(0);

        ProjectionSourceGuardDeliveryResult resumed = await fixture.DeliverAsync(256).ConfigureAwait(true);
        resumed.AlreadyCurrent.ShouldBeTrue();
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(4);
        fixture.Rows[route].ActiveReservation.ShouldBeNull();
        fixture.Rows[route].IdleBindingPending.ShouldBeNull();
        fixture.Handlers.Single().Calls.ShouldBe(3);
        fixture.Handlers.Single().Dispatches.ShouldAllBe(id => id == fixture.Events[3].MessageId);
        var receiptRequest = new ProjectionAppliedReceiptRequest(
            new(fixture.Identity.TenantId, fixture.Identity.Domain, fixture.Identity.AggregateId, route, fixture.Catalog.CatalogFingerprint),
            2, fixture.Events[1].MessageId, "owner-1", "generation-1");
        ProjectionOwnerProofResult recovered = await fixture.Models.ReadAppliedReceiptAsync(receiptRequest, new("models", route)).ConfigureAwait(true);
        recovered.Proof.ShouldNotBeNull().ReceiptFingerprint.ShouldBe(fingerprint);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route).ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(4);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", indexKey).ConfigureAwait(true)).Value.ShouldNotBeNull().Count.ShouldBe(4);
    }

    [Theory]
    [InlineData(10000, 9999, 1, 1, 3, "single-event-v1")]
    [InlineData(10000, 9999, 256, 1, 3, "single-event-v1")]
    [InlineData(10000, 9999, 1, 3, 7, "single-event-v1")]
    [InlineData(9771, 9000, 256, 1, 2313, "single-event-v1")]
    [InlineData(9771, 9000, 1, 1, 2313, "single-event-v1")]
    [InlineData(10000, 9999, 1, 1, 3, "mapped-incremental-replay-v1")]
    [InlineData(10000, 9999, 256, 1, 3, "mapped-incremental-replay-v1")]
    [InlineData(10000, 9999, 1, 3, 7, "mapped-incremental-replay-v1")]
    [InlineData(9771, 9000, 256, 1, 2313, "mapped-incremental-replay-v1")]
    [InlineData(9771, 9000, 1, 1, 2313, "mapped-incremental-replay-v1")]
    public async Task GuardedTail_ProductionDispatchAndStoreMatchCanonicalReplayWithinSourcePassCeilings(
        int head, int completed, int pageSize, int routes, int expectedReturned, string handlerSemantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(head, completed, routes, handlerSemantics);
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(pageSize);
        result.AlreadyCurrent.ShouldBeTrue();
        fixture.ReturnedSequences.Count.ShouldBe(expectedReturned);
        fixture.ReturnedSequences.ShouldAllBe(sequence => sequence > completed && sequence <= head);
        fixture.Ranges.Count.ShouldBe((2 * routes + 1) * ((head - completed + pageSize - 1) / pageSize));
        fixture.Handlers.Sum(static handler => handler.Calls).ShouldBe(routes * (head - completed));
        fixture.RowWrites.ShouldBe(3 * routes);
        AggregateReconstructionResult oracle = AggregateReplayer.Replay<CounterState>(new(fixture.Identity.TenantId,
            fixture.Identity.Domain, "Counter", fixture.Identity.AggregateId, head,
            [.. fixture.Events.Select(static item => new ReplayEventEnvelope(item.SequenceNumber, item.EventTypeName,
                item.Payload, item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId))], false, null));
        oracle.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        using JsonDocument replay = JsonDocument.Parse(oracle.StateJson!);
        int expected = replay.RootElement.GetProperty("count").GetInt32();
        foreach (string route in fixture.Catalog.OrdinaryProjectionTypes)
        {
            GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)).Value.ShouldNotBeNull();
            GuardedTailModel index = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")).Value.ShouldNotBeNull();
            model.Count.ShouldBe(expected);
            index.ShouldBe(model);
            fixture.Rows[route].LastDeliveredSequence.ShouldBe(head);
            fixture.Rows[route].GuardEvidence.ShouldNotBeNull().OwnerProof.Selection.ShouldBe(model.ProjectionOwner);
            fixture.Rows[route].ActiveReservation.ShouldBeNull();
            fixture.Rows[route].IdleBindingPending.ShouldBeNull();
            fixture.Rows[route].CompletedMessageIdDigests!.Count.ShouldBe(head);
            fixture.Handlers.Single(handler => handler.ProjectionType == route).Dispatches.ShouldAllBe(id => id == fixture.Head.HeadMessageId);
        }
        fixture.Work.ShouldBeNull();
        int priorRanges = fixture.Ranges.Count, priorWrites = fixture.RowWrites;
        (await fixture.Probe.TryDeliverAsync(fixture.Identity, CancellationToken.None)).AlreadyCurrent.ShouldBeTrue();
        fixture.Ranges.Count.ShouldBe(priorRanges);
        fixture.RowWrites.ShouldBe(priorWrites);
    }

    [Theory]
    [InlineData("single-event-v1")]
    [InlineData("mapped-incremental-replay-v1")]
    public async Task GuardedTail_LostEventResponseResumesOriginalReservationAndSkipsDurablePrefix(string handlerSemantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1, handlerSemantics: handlerSemantics);
        fixture.Http.LoseResponseAfterSequence = 2;
        (await fixture.DeliverAsync(1)).AlreadyCurrent.ShouldBeFalse();
        ProjectionDeliveryState first = fixture.Rows.Values.Single();
        first.LastDeliveredSequence.ShouldBe(1);
        ProjectionDeliveryReservation original = first.ActiveReservation.ShouldNotBeNull();
        fixture.Work.ShouldNotBeNull().DispatchId.ShouldBe(original.DispatchId);
        fixture.Handlers.Single().Calls.ShouldBe(1);
        (await fixture.DeliverAsync(1)).AlreadyCurrent.ShouldBeTrue();
        fixture.Handlers.Single().Calls.ShouldBe(3);
        fixture.Handlers.Single().Dispatches.ShouldAllBe(id => id == original.DispatchId);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(4);
        GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models",
            fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes[0])).Value.ShouldNotBeNull();
        model.Count.ShouldBe(4);
        fixture.Work.ShouldBeNull();
    }

    [Fact]
    public async Task GuardedTail_ChangedApplicationRereadRejectsBeforeThatEventsHandler()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1);
        fixture.ChangeApplicationSequence = 3;
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("source_conflict");
        fixture.Handlers.Single().Calls.ShouldBe(1);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        fixture.Rows.Values.Single().ActiveReservation.ShouldNotBeNull();
        GuardedTailModel model = (await fixture.Models.GetAsync<GuardedTailModel>("models",
            fixture.Identity.ActorId + ":" + fixture.Catalog.OrdinaryProjectionTypes[0])).Value.ShouldNotBeNull();
        model.Count.ShouldBe(2);
        fixture.Work.ShouldNotBeNull();
    }

    [Fact]
    public async Task GuardedTail_ConflictingOriginalReceiptRetainsReservationAndNeverAppliesLaterEvents()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1);
        fixture.Http.LoseResponseAfterSequence = 2;
        (await fixture.DeliverAsync(1)).AlreadyCurrent.ShouldBeFalse();
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        var scope = new ReadModelBatchScope("models", fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, route, fixture.Events[1].MessageId);
        fixture.Backend.SeedByteStore(scope.ComputeMarkerKey(), "{}"u8.ToArray());
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        fixture.Handlers.Single().Calls.ShouldBe(1);
        fixture.Rows[route].LastDeliveredSequence.ShouldBe(1);
        fixture.Rows[route].ActiveReservation.ShouldNotBeNull();
        fixture.Work.ShouldNotBeNull();
    }

    [Fact]
    public async Task GuardedTail_SourceGrowthCompletesOnlyFrozenHeadAndReportsStaleUntilLaterWork()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1);
        fixture.GrowOnInspectionNumber = 3;
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("head_changed");
        fixture.Head.HeadSequence.ShouldBe(5);
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(4);
        fixture.Rows.Values.Single().GuardEvidence.ShouldNotBeNull().Source.HeadSequence.ShouldBe(4);
        fixture.Handlers.Single().Calls.ShouldBe(3);
        fixture.Work.ShouldBeNull();
        (await fixture.DeliverAsync(1)).AlreadyCurrent.ShouldBeTrue();
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(5);
        fixture.Handlers.Single().Calls.ShouldBe(4);
    }

    [Fact]
    public async Task GuardedTail_AllSiblingsVerifyBeforeTheSharedFinalizationDeadlineStarts()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1, routeCount: 2);
        fixture.HistoryReadDelay = TimeSpan.FromSeconds(31);
        fixture.ReleaseDelay = TimeSpan.FromSeconds(1);
        fixture.CleanupDelay = TimeSpan.FromSeconds(1);
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1);
        result.AlreadyCurrent.ShouldBeTrue();
        fixture.Work.ShouldBeNull();
        fixture.ReturnedSequences.Count.ShouldBe(5);
        fixture.Handlers.ShouldAllBe(static handler => handler.Calls == 1);
        fixture.Rows.Values.ShouldAllBe(static row => row.LastDeliveredSequence == 2 && row.IdleBindingPending == null);
    }

    [Fact]
    public async Task GuardedTail_FinalizationAndSchedulerCleanupShareOneThirtySecondElapsedBudget()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(2, 1);
        DateTimeOffset began = fixture.Clock.GetUtcNow();
        fixture.ReleaseDelay = TimeSpan.FromSeconds(29);
        fixture.CleanupDelay = TimeSpan.FromSeconds(1);
        ProjectionSourceGuardDeliveryResult result = await fixture.DeliverAsync(1);
        result.AlreadyCurrent.ShouldBeFalse();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        (fixture.Clock.GetUtcNow() - began).ShouldBe(TimeSpan.FromSeconds(30));
        fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(2);
        fixture.Rows.Values.Single().ActiveReservation.ShouldBeNull();
        fixture.Work.ShouldNotBeNull("expired cleanup must retain the original work rather than starting a new deadline");
        fixture.Handlers.Single().Calls.ShouldBe(1);
    }

    [Fact]
    public async Task GuardedTail_NinthActivationRemainsPendingWhenBackoffAttemptSaturatesAtEight()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(4, 1);
        fixture.Http.RejectDispatch = true;
        int initialWrites = fixture.Backend.TrySaveByteOperations.Count;
        string? dispatch = null, manifest = null;
        for (int activation = 1; activation <= 9; activation++)
        {
            (await fixture.DeliverAsync(1)).AlreadyCurrent.ShouldBeFalse();
            ProjectionDeliveryRetryWorkItem work = fixture.Work.ShouldNotBeNull();
            work.Attempt.ShouldBe(Math.Min(activation, 8));
            work.PendingRoutes.ShouldHaveSingleItem();
            work.TerminalRoutes.ShouldBeEmpty();
            ProjectionDeliveryReservation reservation = fixture.Rows.Values.Single().ActiveReservation.ShouldNotBeNull();
            dispatch ??= reservation.DispatchId;
            manifest ??= reservation.ManifestFingerprint;
            reservation.DispatchId.ShouldBe(dispatch);
            reservation.ManifestFingerprint.ShouldBe(manifest);
            reservation.FencingToken.ShouldBe(activation);
            fixture.Rows.Values.Single().LastDeliveredSequence.ShouldBe(1);
        }
        fixture.Work.ShouldNotBeNull().NextDueUtc.ShouldBe(fixture.Clock.GetUtcNow() + TimeSpan.FromSeconds(128));
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(initialWrites);
    }
}
