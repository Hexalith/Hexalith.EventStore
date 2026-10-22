namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — operator-initiated request to invalidate or delete protection keys for the scoped
/// data. EventStore records the workflow decision; provider-specific crypto execution remains out
/// of scope.
/// </summary>
/// <param name="Identity">Stable idempotency key for the workflow.</param>
/// <param name="RequestedAction">The requested terminal action (<see cref="CryptoShreddingWorkflowState.Invalidated"/> or <see cref="CryptoShreddingWorkflowState.Deleted"/>).</param>
/// <param name="OperatorActorId">The operator who submitted the request.</param>
/// <param name="CorrelationId">Optional correlation identifier.</param>
/// <param name="SubmittedAtUtc">When the request was submitted.</param>
public sealed record CryptoShreddingWorkflowRequest(
    CryptoShreddingWorkflowIdentity Identity,
    CryptoShreddingWorkflowState RequestedAction,
    string OperatorActorId,
    string? CorrelationId,
    DateTimeOffset SubmittedAtUtc) {
    /// <summary>Validates the workflow request.</summary>
    /// <param name="rejectionReason">A short human-readable rejection reason when validation fails.</param>
    /// <returns><see langword="true"/> when the request is valid.</returns>
    public bool TryValidate(out string? rejectionReason) {
        ArgumentNullException.ThrowIfNull(Identity);
        if (!Identity.TryValidate(out rejectionReason)) {
            return false;
        }

        if (RequestedAction is not (CryptoShreddingWorkflowState.Invalidated or CryptoShreddingWorkflowState.Deleted)) {
            rejectionReason = "RequestedAction must be Invalidated or Deleted.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OperatorActorId)) {
            rejectionReason = "OperatorActorId is required.";
            return false;
        }

        rejectionReason = null;
        return true;
    }
}
