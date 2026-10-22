using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Explicit mutating recovery of an existing event batch, separate from read-only current proof.</summary>
public interface IReadModelAppliedReceiptRecoveryStore
{
    /// <summary>Resumes only the durable original manifest under its original event/batch identity.</summary>
    Task<ProjectionOwnerProofResult> ReconcileAppliedReceiptAsync(ProjectionAppliedReceiptRequest request,
        ReadModelOwnerProofBinding binding, CancellationToken cancellationToken = default);
}
