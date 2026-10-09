namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact deletion request and ordinal installed when one source write was accepted.</summary>
/// <param name="DeletionRequestId">The installed deletion request.</param>
/// <param name="Ordinal">Its ordinal at the write's own linearization.</param>
public sealed record GovernanceAdmissionAttribution(string DeletionRequestId, long Ordinal);
