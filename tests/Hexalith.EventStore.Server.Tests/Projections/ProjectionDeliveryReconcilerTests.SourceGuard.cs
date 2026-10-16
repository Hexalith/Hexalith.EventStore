using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryReconcilerTests
{
    [Fact]
    public async Task SourceGuardQualification_CompletedSameCheckpointRebuildRehydratesOnlyTheNewSelectedReceiptAndIdleBinding()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3,
            handlerSemantics: "full-replay-v1").ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var store = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        ProjectionDeliveryState original = (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).State!;
        byte[] marker = fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.GetSourceGuardKey(fixture.Identity, route));
        byte[] writer = fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.WriterProtocol);
        fixture.Clock.SetUtcNow(original.UpdatedAt.AddMinutes(1));

        await fixture.CompleteOperatorRebuildAsync("rebuilt-generation-2").ConfigureAwait(true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), false).ConfigureAwait(true);

        result.ShouldBe(new(true, false, "delivery_reconciled", 3));
        ProjectionDeliveryState hydrated = (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).State!;
        hydrated.LastDeliveredSequence.ShouldBe(original.LastDeliveredSequence);
        hydrated.LastCompletedMessageId.ShouldBe(original.LastCompletedMessageId);
        hydrated.CompletedPrefixFingerprint.ShouldBe(original.CompletedPrefixFingerprint);
        hydrated.CompletedMessageIdDigests.ShouldBe(original.CompletedMessageIdDigests);
        hydrated.CompletedReceipts.ShouldBe(original.CompletedReceipts);
        hydrated.FirstRetainedSequence.ShouldBe(original.FirstRetainedSequence);
        hydrated.ActiveReservation.ShouldBeNull();
        hydrated.IdleBindingPending.ShouldBeNull();
        hydrated.GuardEvidence.ShouldNotBeNull().Source.ShouldBe(original.GuardEvidence!.Source);
        hydrated.GuardEvidence.OwnerProof.Selection.OwnerGeneration.ShouldBe("rebuilt-generation-2");
        hydrated.GuardEvidence.OwnerProof.Selection.ReceiptProjectionName.ShouldBe("rebuild");
        hydrated.GuardEvidence.IdleLifecycleRevision.ShouldBeGreaterThan(original.GuardEvidence.IdleLifecycleRevision);
        hydrated.UpdatedAt.ShouldBeGreaterThan(original.UpdatedAt);
        fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.GetSourceGuardKey(fixture.Identity, route)).ShouldBe(marker);
        fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.WriterProtocol).ShouldBe(writer);
        fixture.Handlers.Single().Calls.ShouldBe(0);
        fixture.Handlers.Single().RebuildCalls.ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(3);
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(1);
        ProjectionDeliveryReconciliationWork audit = (await store.ReadReconciliationAsync(fixture.Identity, route).ConfigureAwait(true))!;
        audit.SourceGuardMaintenance.ShouldNotBeNull().OperatorId.ShouldBe("operator-7");
    }

    [Fact]
    public async Task SourceGuardQualification_ExactSupportedScopeDoesNotRequireUnrelatedSiblingAdmission()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3, 2,
            unguardedLastRoute: true).ConfigureAwait(true);
        string route = fixture.Catalog.OrdinaryProjectionTypes[0];
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture, route);

        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);

        result.ShouldBe(new(true, true, "delivery_reconciled", 3));
        (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).SourceGuard.ShouldNotBeNull();
        (await store.ReadAsync(fixture.Identity, fixture.Catalog.OrdinaryProjectionTypes[1]).ConfigureAwait(true)).SourceGuard.ShouldBeNull();
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 0 && handler.RebuildCalls == 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceGuardQualification_ExplicitNewEmptyScopeInitializesZeroWithoutModelOrCompletionReceipt(bool activate)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(0, 0).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 0, fixture.Head.SourceIncarnation, ValidMaintenance(), activate).ConfigureAwait(true);
        result.ShouldBe(new(true, activate, "empty_no_work", 0));
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        ProjectionDeliveryState row = read.State.ShouldNotBeNull();
        row.SchemaVersion.ShouldBe(2);
        row.LastDeliveredSequence.ShouldBe(0);
        row.GuardEvidence.ShouldBeNull();
        row.CompletedReceipts.ShouldBeEmpty();
        row.CompletedMessageIdDigests.ShouldBeEmpty();
        (read.SourceGuard is not null).ShouldBe(activate);
        fixture.ReturnedSequences.ShouldBeEmpty();
        fixture.Handlers.Sum(static handler => handler.Calls).ShouldBe(0);
        (await fixture.Models.ReadOwnerProofAsync(new(fixture.Identity.TenantId, fixture.Identity.Domain, fixture.Identity.AggregateId,
            route, fixture.Catalog.CatalogFingerprint), new("models", route)).ConfigureAwait(true)).Proof.ShouldBeNull();
    }

    [Fact]
    public async Task SourceGuardQualification_NonemptySourceCannotInventFreshZeroOwnerProgress()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 0).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeFalse();
        result.ReasonCode.ShouldBe("delivery_reconciliation_required");
        ProjectionDeliveryStateReadResult unchanged = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        unchanged.State!.SchemaVersion.ShouldBe(original.SchemaVersion);
        unchanged.State.LastDeliveredSequence.ShouldBe(0);
        unchanged.State.GuardEvidence.ShouldBeNull();
        unchanged.SourceGuard.ShouldBeNull();
        fixture.Backend.TransactionOperations.ShouldBeNull();
        fixture.ReturnedSequences.ShouldBeEmpty();
    }

    [Fact]
    public async Task SourceGuardQualification_MappedAppendDescriptorAndCatalogRequireRealSelectedReceiptBeforeActivation()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3,
            handlerSemantics: "mapped-incremental-replay-v1").ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeTrue();
        result.Activated.ShouldBeTrue();
        (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).SourceGuard.ShouldNotBeNull()
            .HandlerSemantics.ShouldBe("mapped-incremental-replay-v1");
        fixture.Handlers.Sum(static handler => handler.Calls).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceGuardQualification_PreservesCheckpointAndEveryIdentityProof_WithExactScopeAdmission(bool activate)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(5, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        byte[] writerBefore = fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.WriterProtocol);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardMaintenance maintenance = ValidMaintenance();
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity, route,
            fixture.Head.HeadSequence, fixture.Head.SourceIncarnation, maintenance, activate).ConfigureAwait(true);

        result.ShouldBe(new(true, activate, "delivery_reconciled", 3));
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        ProjectionDeliveryState state = read.State.ShouldNotBeNull();
        state.SchemaVersion.ShouldBe(2);
        state.GuardEvidenceVersion.ShouldBe(1);
        state.LastDeliveredSequence.ShouldBe(original.LastDeliveredSequence);
        state.LastCompletedMessageId.ShouldBe(original.LastCompletedMessageId);
        state.CompletedPrefixFingerprint.ShouldBe(original.CompletedPrefixFingerprint);
        state.CompletedReceipts.ShouldBe(original.CompletedReceipts);
        state.FirstRetainedSequence.ShouldBe(original.FirstRetainedSequence);
        state.CompletedMessageIdDigests.ShouldBe(original.CompletedMessageIdDigests);
        state.GuardEvidence.ShouldNotBeNull().Source.HeadSequence.ShouldBe(3);
        state.GuardEvidence.OwnerProof.ShouldBe(fixture.Rows[route].GuardEvidence!.OwnerProof);
        state.GuardEvidence.IdleLifecycleRevision.ShouldBe(1);
        (read.SourceGuard is not null).ShouldBe(activate);
        read.StateBytes.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(state, fixture.Backend.JsonSerializerOptions).Length);
        ProjectionDeliveryReconciliationWork audit = (await store.ReadReconciliationAsync(fixture.Identity, route).ConfigureAwait(true)).ShouldNotBeNull();
        audit.OperatorId.ShouldBe(maintenance.OperatorId);
        audit.SourceGuardMaintenance.ShouldNotBeNull().WriterInventory.ShouldBe(maintenance.WriterInventory);
        audit.QualifiedSourceGuard.ShouldNotBeNull().SpecSha256.ShouldBe(maintenance.SpecSha256);
        fixture.ReturnedSequences.ShouldBe([1, 2, 3, 4, 5]);
        fixture.Ranges.Count.ShouldBe(1);
        fixture.Handlers.Sum(static handler => handler.Calls).ShouldBe(0);
        fixture.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.WriterProtocol).ShouldBe(writerBefore);
        fixture.Backend.TransactionOperations.Count.ShouldBe(activate ? 3 : 2);
        fixture.Backend.TransactionOperations[0].Options.Concurrency.ShouldBe(Dapr.Client.ConcurrencyMode.FirstWrite);
        if (activate) fixture.Backend.TransactionOperations[1].Options.Concurrency.ShouldBe(Dapr.Client.ConcurrencyMode.FirstWrite);
        cost[ProjectionDeliveryCost.GuardActivationAttempts].ShouldBe(activate ? 1 : 0);
        cost[ProjectionDeliveryCost.ReconciliationWrites].ShouldBe(1);
        cost[ProjectionDeliveryCost.ReconciliationReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.TransactionAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ProofWriteBytes].ShouldBe(read.StateBytes);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(activate ? 2 : 1);
        cost[ProjectionDeliveryCost.MetadataSerializationBytes].ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("append")]
    [InlineData("erasure")]
    public async Task SourceGuardQualification_RejectsIncompleteFleetBeforeAnySourceWork(string omittedRole)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture);
        ProjectionSourceGuardMaintenance valid = ValidMaintenance();
        ProjectionSourceGuardMaintenance incomplete = valid with { WriterInventory = [.. valid.WriterInventory.Where(item => item.Role != omittedRole)] };
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            fixture.Catalog.OrdinaryProjectionTypes.Single(), 3, fixture.Head.SourceIncarnation, incomplete, true).ConfigureAwait(true);
        result.Qualified.ShouldBeFalse();
        result.ReasonCode.ShouldBe("guard_activation_required");
        fixture.SourceInspections.ShouldBe(0);
        fixture.ReturnedSequences.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("mapped-incremental-replay-v1")]
    [InlineData("shared-epoch-v1")]
    public async Task SourceGuardQualification_CannotActivateAnUnimplementedRuntimeProtocol(string semantics)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        var capability = new ProjectionSourceGuardCapability(1, 1, 1, semantics, "projection-owner-proof/v1",
            semantics == "full-replay-v1" ? null : semantics == "shared-epoch-v1" ? "source-sequence-position/v1" : "event-message-id/v1",
            semantics == "full-replay-v1" ? null : semantics == "shared-epoch-v1" ? "shared-applied/v1" : "mapped-append/v1");
        var catalog = new NamedProjectionRouteCatalogEntry("sample", "v1", fixture.Identity.Domain, 2,
            ProjectionDispatchProtocol.Capability, fixture.Catalog.CatalogFingerprint, [route])
        { SourceGuardCapabilities = new Dictionary<string, ProjectionSourceGuardCapability>(StringComparer.Ordinal) { [route] = capability } };
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store, catalog).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.ReasonCode.ShouldBe("unsupported_capability");
        (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).SourceGuard.ShouldBeNull();
        fixture.SourceInspections.ShouldBe(0);
    }

    [Fact]
    public async Task SourceGuardQualification_FullReplayRequiresItsDeclaredCatalogAndPreservesTheExistingCheckpoint()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3, 1,
            "full-replay-v1").ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeTrue();
        result.Activated.ShouldBeTrue();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        read.SourceGuard.ShouldNotBeNull().HandlerSemantics.ShouldBe("full-replay-v1");
        read.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(original.LastDeliveredSequence);
        read.State.CompletedMessageIdDigests.ShouldBe(original.CompletedMessageIdDigests);
        fixture.Handlers.ShouldAllBe(handler => handler.Calls == 0 && handler.RebuildCalls == 0);
    }

    [Fact]
    public async Task SourceGuardQualification_MissingSelectedReceiptCannotBeHydratedFromHistory()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string receipt = new Hexalith.EventStore.Client.Projections.ReadModelBatchScope("models", fixture.Identity.TenantId,
            fixture.Identity.Domain, fixture.Identity.AggregateId, route, fixture.Head.HeadMessageId!).ComputeMarkerKey();
        fixture.Backend.SeedByteStore(receipt, "{}"u8.ToArray());
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeFalse();
        fixture.ReturnedSequences.ShouldBeEmpty();
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        read.State!.SchemaVersion.ShouldBe(original.SchemaVersion);
        read.SourceGuard.ShouldBeNull();
    }

    [Fact]
    public async Task SourceGuardQualification_HeadRaceBeforeHydrationPreservesTheUnqualifiedRow()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        fixture.GrowOnInspectionNumber = 2;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.ReasonCode.ShouldBe("head_changed");
        ProjectionDeliveryStateReadResult read = await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true);
        read.State!.SchemaVersion.ShouldBe(original.SchemaVersion);
        read.SourceGuard.ShouldBeNull();
    }

    [Fact]
    public async Task SourceGuardQualification_LostTransactionResponseRequiresRowMarkerAndAttributionReadback()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, _) = SeedQualification(fixture);
        fixture.Backend.AfterStateTransaction = _ => throw new IOException("Lost response.");
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeTrue();
        result.Activated.ShouldBeTrue();
        (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).SourceGuard.ShouldNotBeNull();
        (await store.ReadReconciliationAsync(fixture.Identity, route).ConfigureAwait(true)).ShouldNotBeNull().SourceGuardMaintenance.ShouldNotBeNull();
    }

    [Fact]
    public async Task SourceGuardQualification_CompetingRowCASCannotPublishScopeAdmissionOrRepeatHistory()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 3).ConfigureAwait(true);
        (DaprProjectionDeliveryStateStore store, ProjectionDeliveryState original) = SeedQualification(fixture);
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        fixture.Backend.BeforeStateTransaction = _ => fixture.Backend.SeedByteStore(
            ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route),
            JsonSerializer.SerializeToUtf8Bytes(original with { UpdatedAt = original.UpdatedAt.AddSeconds(1) }, fixture.Backend.JsonSerializerOptions));
        ProjectionSourceGuardQualificationResult result = await fixture.CreateQualification(store).QualifyAsync(fixture.Identity,
            route, 3, fixture.Head.SourceIncarnation, ValidMaintenance(), true).ConfigureAwait(true);
        result.Qualified.ShouldBeFalse();
        (await store.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).SourceGuard.ShouldBeNull();
        fixture.ReturnedSequences.Count.ShouldBe(3);
        fixture.Ranges.Count.ShouldBe(1);
    }

    private static (DaprProjectionDeliveryStateStore Store, ProjectionDeliveryState Original) SeedQualification(GuardedTailProductionHarness fixture,
        string? projection = null)
    {
        fixture.Backend.EnforceTransactionConcurrency = true;
        string route = projection ?? fixture.Catalog.OrdinaryProjectionTypes.Single();
        ProjectionDeliveryState original = fixture.Rows[route] with { SchemaVersion = 1, GuardEvidence = null, GuardEvidenceVersion = null };
        fixture.Backend.SeedByteStore(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route),
            JsonSerializer.SerializeToUtf8Bytes(original, fixture.Backend.JsonSerializerOptions));
        fixture.Backend.SeedByteStore(ProjectionDeliveryStateKeys.WriterProtocol, JsonSerializer.SerializeToUtf8Bytes(
            new ProjectionDeliveryWriterProtocol(1, 2, "original-writer-v2-cutover", fixture.Clock.GetUtcNow()), fixture.Backend.JsonSerializerOptions));
        return (new(fixture.Backend, Options.Create(new ProjectionOptions())), original);
    }

    private static ProjectionSourceGuardMaintenance ValidMaintenance()
        => new("operator-7", new string('b', 40), "consistent-backup-2026-10-15", ProjectionSourceGuard.ApprovedSpecSha256,
            true, true, true, [.. new[] { "append", "immediate", "poller", "retry", "reconciliation", "rebuild", "erasure" }
                .Select(static role => new ProjectionSourceGuardWriterInventory(role, new string('b', 40), "package@1.0.0+immutable-sha256", true))]);
}
