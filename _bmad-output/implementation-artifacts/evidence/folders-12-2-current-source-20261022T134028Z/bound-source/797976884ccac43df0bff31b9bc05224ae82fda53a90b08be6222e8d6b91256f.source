using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Read-only access to the existing immutable per-event terminal batch receipt.</summary>
public interface IReadModelAppliedReceiptStore
{
    /// <summary>Reads one original receipt, without model reconstruction, repair or new manifests.</summary>
    Task<ProjectionOwnerProofResult> ReadAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default);
}
