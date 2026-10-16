using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

public sealed partial class DaprReadModelStore : IReadModelAppliedReceiptStore
{
    /// <inheritdoc/>
    public async Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(binding);
        var identity = new AggregateIdentity(request.Scope.TenantId, request.Scope.Domain, request.Scope.AggregateId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SequenceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OwnerIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OwnerGeneration);
        var scope = new ReadModelBatchScope(binding.StoreName, identity.TenantId, identity.Domain,
            identity.AggregateId, request.Scope.ProjectionName, request.MessageId);
        var accessor = new DaprReadModelBatchStateAccessor(_daprClient, binding.StoreName);
        long bytes = 0;
        try
        {
            RawStateEntry row = await accessor.ReadAsync(scope.ComputeMarkerKey(), cancellationToken).ConfigureAwait(false);
            bytes = row.Value.Length;
            if (!row.Exists || bytes > 16_384) return new(null, "delivery_reconciliation_required", 1, bytes);
            ReadModelBatchMarker? receipt = ReadModelBatchMarker.ReadProof(row.Value);
            if (receipt is not { Version: 1, Status: ReadModelBatchMarkerStatus.Completed, Operations: null, OwnerSelection: { } selected }
                || receipt.OriginalManifest is not null || receipt.OriginalManifestKey is not null || receipt.OwnerSelections is not null
                || receipt.ScopeHash != scope.ComputeScopeHash() || receipt.BatchId != request.MessageId
                || !ReadModelBatchFingerprint.IsCanonical(receipt.Fingerprint) || string.IsNullOrWhiteSpace(receipt.TerminalTimeUtc)
                || selected.Version != 1 || selected.TenantId != identity.TenantId || selected.Domain != identity.Domain
                || selected.AggregateId != identity.AggregateId || selected.ProjectionName != request.Scope.ProjectionName
                || selected.AppliedSequence != request.SequenceNumber || selected.MessageId != request.MessageId
                || selected.BatchId != request.MessageId || selected.OwnerIdentity != request.OwnerIdentity
                || selected.OwnerGeneration != request.OwnerGeneration || string.IsNullOrWhiteSpace(selected.FreshnessVersion))
                return new(null, "delivery_reconciliation_required", 1, bytes);
            return new(new(selected, receipt.ScopeHash, receipt.Fingerprint), null, 1, bytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return new(null, "delivery_reconciliation_required", 1, bytes);
        }
        catch { return new(null, "delivery_state_unavailable", 1, bytes); }
    }
}
