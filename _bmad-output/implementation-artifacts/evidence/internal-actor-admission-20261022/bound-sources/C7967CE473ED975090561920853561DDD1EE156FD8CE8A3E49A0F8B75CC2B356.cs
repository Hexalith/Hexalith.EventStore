namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact operation scope authenticated before protected lookup or mutation.</summary>
/// <param name="TenantId">The canonical tenant.</param>
/// <param name="Domain">The domain.</param>
/// <param name="AggregateId">The exact target.</param>
/// <param name="Operation">The fully qualified operation type.</param>
/// <param name="MessageId">The delivery identity, or query correlation identity.</param>
/// <param name="LogicalId">The stable logical intent identity.</param>
/// <param name="PayloadDigest">SHA-256 of the exact delivered payload.</param>
public sealed record IdentityAdmissionScope(string TenantId, string Domain, string AggregateId,
    string Operation, string MessageId, string LogicalId, string PayloadDigest);
