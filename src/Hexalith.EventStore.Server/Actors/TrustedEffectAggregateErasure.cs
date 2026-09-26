namespace Hexalith.EventStore.Server.Actors;

/// <summary>Identifies one tenant-owned aggregate for a signed deletion fence or final erasure.</summary>
/// <param name="Tenant">Canonical tenant identifier.</param>
/// <param name="Domain">Canonical domain identifier.</param>
/// <param name="Aggregate">Aggregate identifier.</param>
/// <param name="EffectIds">Every registered effect targeting this aggregate.</param>
/// <param name="InventoryDigest">Digest of the complete lifecycle effect inventory.</param>
/// <param name="DeletionApprovedAt">Persisted accountable deletion decision timestamp.</param>
/// <param name="IssuedAt">Time the lifecycle issued this capability.</param>
/// <param name="ExpiresAt">Short-lived capability expiry.</param>
/// <param name="Nonce">Random one-use decision nonce for this partition.</param>
/// <param name="Capability">Lifecycle-issued partition-bound signature.</param>
public sealed record TrustedEffectAggregateErasure(
    string Tenant,
    string Domain,
    string Aggregate,
    string[] EffectIds,
    string InventoryDigest,
    DateTimeOffset DeletionApprovedAt,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string Nonce,
    string Capability)
{
    /// <summary>Capability purpose for a durable deletion-entry fence.</summary>
    public const string DeletionFencePurpose = "DeletionFence";

    /// <summary>Capability purpose for final evidence erasure.</summary>
    public const string ErasurePurpose = "PurgeEligible";

    /// <summary>Domain-separated lifecycle operation authorized by this capability.</summary>
    public string Purpose { get; init; } = ErasurePurpose;
}
