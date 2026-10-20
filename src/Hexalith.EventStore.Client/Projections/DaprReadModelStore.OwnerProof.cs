using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelOwnerProofStore
{
    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReadOwnerProofAsync(ProjectionOwnerProofRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(binding);
        var identity = new AggregateIdentity(request.TenantId, request.Domain, request.AggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.StoreName);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.SelectionSuffix);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, binding.StoreName);
        int reads = 0;
        long bytes = 0;
        try
        {
            reads++;
            RawStateEntry model = await accessor.ReadAsync(binding.SelectionKey?.Invoke(request) ?? identity.ActorId + ":" + binding.SelectionSuffix, cancellationToken).ConfigureAwait(false);
            bytes += model.Value.Length;
            if (!model.Exists) return new(null, "delivery_rebuild_required", reads, bytes);
            if (model.Value.Length > 22_016 || ReadModelBatchEnvelope.IsEnvelope(model.Value.Span))
                return new(null, "delivery_reconciliation_required", reads, bytes);
            model = ReadModelBatchExpiringValue.Resolve(model, _options.TimeProvider);
            if (!model.Exists) return new(null, "delivery_rebuild_required", reads, bytes);
            if (model.Value.Length > 16_384)
                return new(null, "delivery_reconciliation_required", reads, bytes);
            ProjectionOwnerSelection? selection = ReadModelOwnerSelectionMetadata.Read(model.Value);
            if (selection is null || selection.TenantId != identity.TenantId || selection.Domain != identity.Domain
                || selection.AggregateId != identity.AggregateId || selection.ProjectionName != request.ProjectionName)
                return new(null, "delivery_reconciliation_required", reads, bytes);
            var scope = new ReadModelBatchScope(binding.StoreName, identity.TenantId, identity.Domain,
                identity.AggregateId, selection.ReceiptProjectionName ?? request.ProjectionName, selection.BatchId);
            reads++;
            RawStateEntry receiptRow = await accessor.ReadAsync(scope.ComputeMarkerKey(), cancellationToken).ConfigureAwait(false);
            bytes += receiptRow.Value.Length;
            if (!receiptRow.Exists)
                return new(null, "delivery_reconciliation_required", reads, bytes);
            ReadModelBatchMarker? receipt = ReadModelBatchMarker.ReadBoundedProof(receiptRow.Value);
            if (receipt is null || receipt.Version != 1 || receipt.Status != ReadModelBatchMarkerStatus.Completed
                || receipt.ScopeHash != scope.ComputeScopeHash() || receipt.BatchId != selection.BatchId
                || !ReadModelOwnerSelectionMetadata.ReceiptContains(receipt, selection) || receipt.Operations is not null
                || receipt.OriginalManifest is not null || receipt.OriginalManifestKey is not null
                || !ReadModelBatchFingerprint.IsCanonical(receipt.Fingerprint) || string.IsNullOrWhiteSpace(receipt.TerminalTimeUtc))
                return new(null, "delivery_reconciliation_required", reads, bytes);
            if (model.ExpiresAtUtc is { } expiry && expiry <= _options.TimeProvider.GetUtcNow())
                return new(null, "delivery_rebuild_required", reads, bytes);
            return new(new(selection, receipt.ScopeHash, receipt.Fingerprint), null, reads, bytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return new(null, "delivery_reconciliation_required", reads, bytes);
        }
        catch { return new(null, "delivery_state_unavailable", reads, bytes); }
    }
}
