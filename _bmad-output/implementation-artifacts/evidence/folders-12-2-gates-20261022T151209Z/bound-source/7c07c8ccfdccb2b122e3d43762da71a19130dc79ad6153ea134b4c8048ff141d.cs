using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Additive read-only access to selected owner metadata and existing batch receipts.</summary>
public interface IReadModelOwnerProofStore
{
    /// <summary>Reads selected output and its receipt without repair, compaction or domain handler invocation.</summary>
    Task<ProjectionOwnerProofResult> ReadOwnerProofAsync(ProjectionOwnerProofRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default);
}
