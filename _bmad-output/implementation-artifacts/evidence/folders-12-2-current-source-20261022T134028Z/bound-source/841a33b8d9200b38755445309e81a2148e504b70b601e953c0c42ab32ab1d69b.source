using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public partial class DaprReadModelBatchTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 1)]
    [InlineData(ReadModelBatchPhase.RecoveryReceiptWritten, 1)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 1)]
    public async Task ContentFreeSingleOwnerRecoveryRetainsTheOriginalNativeSelectionAndReceipt(ReadModelBatchPhase phase, int ordinal)
    {
        using var client = new RecordingDaprClient();
        DaprReadModelStore store = CreateStore(client);
        ReadModelBatchScope scope = Scope();
        string key = "private-content-free-selection";
        var selection = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId,
            scope.ProjectionType, "exact-owner", "immutable-generation", 2, "actual-source-message-2", scope.BatchId, "native-version");
        var batch = new ReadModelBatch(scope,
        [
            ReadModelBatchOperation.Write("private-content-free-fence", new { generation = "immutable-generation" }, ReadModelBatchConcurrency.CreateOnly),
            ReadModelBatchOperation.Write(key, new { ProjectionVersion = selection.FreshnessVersion, ProjectionOwner = selection }, ReadModelBatchConcurrency.CreateOnly),
        ]) { OwnerSelection = selection, RetainManifestForRecovery = true };
        // The test adapter uses the same existing SDK protocol. Fault injection mutates only the test backend.
        var options = new ReadModelBatchOptions();
        var injector = new NativeOwnerRecoveryFault(phase, ordinal);
        var protocol = new ReadModelBatchProtocol(new DaprReadModelBatchStateAccessor(client, scope.StoreName), options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch, injector, TestContext.Current.CancellationToken));
        injector.Fired.ShouldBeTrue();
        var binding = new ReadModelOwnerProofBinding(scope.StoreName, "unused-canonical-key") { SelectionKey = _ => key };
        var request = new ProjectionOwnerProofRequest(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog");
        (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldBeNull();
        // Recovery is producer-only; the query proof above has performed no repair.
        foreach (string participant in new[] { "private-content-free-fence", key })
            (await store.RecoverPendingAsync(scope, participant)).IsResolved.ShouldBeTrue();
        var proof = (await store.ReadOwnerProofAsync(request, binding)).Proof.ShouldNotBeNull();
        proof.Selection.ShouldBe(selection);
        proof.ReceiptIdentity.ShouldBe(scope.ComputeScopeHash());
        proof.ReceiptFingerprint.ShouldBe(ReadModelBatchFingerprint.Compute(batch));
        using JsonDocument receipt = JsonDocument.Parse(client.ByteStoreValue(scope.ComputeMarkerKey()));
        receipt.RootElement.TryGetProperty("manifest", out _).ShouldBeFalse();
        receipt.RootElement.TryGetProperty("manifestKey", out _).ShouldBeFalse();
        (await store.GetAsync<Dictionary<string, JsonElement>>(scope.StoreName, key)).Value!["projectionOwner"]
            .Deserialize<ProjectionOwnerSelection>(JsonSerializerOptions.Web).ShouldBe(selection);
    }
}
