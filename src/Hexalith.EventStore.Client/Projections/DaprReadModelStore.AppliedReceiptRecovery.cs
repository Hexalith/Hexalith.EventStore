using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelAppliedReceiptRecoveryStore
{
    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(binding);
        var identity = new AggregateIdentity(request.Scope.TenantId, request.Scope.Domain, request.Scope.AggregateId);
        var scope = new ReadModelBatchScope(binding.StoreName, identity.TenantId, identity.Domain,
            identity.AggregateId, request.Scope.ProjectionName, request.MessageId);
        var accessor = new ReadModelBatchMeasuredStateAccessor(new DaprReadModelBatchStateAccessor(_daprClient, binding.StoreName));
        long bytes = 0;
        try
        {
            RawStateEntry row = await accessor.ReadAsync(scope.ComputeMarkerKey(), cancellationToken).ConfigureAwait(false);
            bytes = row.Value.Length;
            if (!row.Exists) return new(null, "delivery_rebuild_required", 1, bytes);
            if (bytes > 16_384) return new(null, "delivery_proof_limit_exceeded", 1, bytes);
            ReadModelBatchMarker? marker = ReadModelBatchMarker.ReadProof(row.Value);
            if (marker is not { Version: 1, OwnerSelection: { } selection }
                || marker.ScopeHash != scope.ComputeScopeHash() || marker.BatchId != request.MessageId
                || !ReadModelBatchFingerprint.IsCanonical(marker.Fingerprint)
                || selection.TenantId != identity.TenantId || selection.Domain != identity.Domain
                || selection.AggregateId != identity.AggregateId || selection.ProjectionName != request.Scope.ProjectionName
                || selection.AppliedSequence != request.SequenceNumber || selection.MessageId != request.MessageId
                || selection.BatchId != request.MessageId || selection.OwnerIdentity != request.OwnerIdentity
                || selection.OwnerGeneration != request.OwnerGeneration)
                return new(null, "delivery_reconciliation_required", 1, bytes);
            if (marker.Status == ReadModelBatchMarkerStatus.Completed)
                return IncludeRecoveryReads(await ReadAppliedReceiptAsync(request, binding, cancellationToken).ConfigureAwait(false), accessor);
            if (marker.Status is not ReadModelBatchMarkerStatus.Prepared and not ReadModelBatchMarkerStatus.Committed
                || marker.Operations is null || marker.OriginalManifest is not null && marker.OriginalManifestKey is not null)
                return new(null, "delivery_reconciliation_required", 1, bytes);
            ReadOnlyMemory<byte> manifest = marker.OriginalManifest;
            if (marker.OriginalManifestKey is not null)
            {
                if (marker.OriginalManifestKey != ReadModelBatchKeys.ManifestKey(scope.ComputeScopeHash()))
                    return new(null, "delivery_reconciliation_required", accessor.ReadCount, accessor.ReadBytes);
                RawStateEntry storedManifest = await accessor.ReadAsync(marker.OriginalManifestKey, cancellationToken).ConfigureAwait(false);
                if (!storedManifest.Exists) return new(null, "delivery_reconciliation_required", accessor.ReadCount, accessor.ReadBytes);
                manifest = storedManifest.Value;
            }
            if (manifest.IsEmpty || manifest.Length > 1_048_576)
                return new(null, "delivery_proof_limit_exceeded", accessor.ReadCount, accessor.ReadBytes);
            ReadModelBatch original = ReadModelBatchManifestCodec.Decode(manifest, scope, selection, marker.Fingerprint);
            if (marker.Operations.Count != original.Operations.Count || marker.Operations.Where((operation, index) =>
                operation.Ordinal != index || operation.Key != original.Operations[index].Key
                    || operation.IsDelete != (original.Operations[index].Kind == ReadModelBatchOperationKind.Delete)).Any())
                return new(null, "delivery_reconciliation_required", 1, bytes);
            var protocol = new ReadModelBatchProtocol(accessor, _options, _logger);
            ReadModelBatchResult result = await protocol.ExecuteAsync(original, null, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? IncludeRecoveryReads(await ReadAppliedReceiptAsync(request, binding, cancellationToken).ConfigureAwait(false), accessor)
                : new(null, "delivery_reconciliation_required", accessor.ReadCount, accessor.ReadBytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return new(null, "delivery_reconciliation_required", accessor.ReadCount, accessor.ReadBytes);
        }
        catch { return new(null, "delivery_state_unavailable", accessor.ReadCount, accessor.ReadBytes); }
    }

    private static ProjectionOwnerProofResult IncludeRecoveryReads(ProjectionOwnerProofResult result, ReadModelBatchMeasuredStateAccessor accessor)
        => result with { MetadataKeyReads = result.MetadataKeyReads + accessor.ReadCount, ReadBodyBytes = result.ReadBodyBytes + accessor.ReadBytes };
}
