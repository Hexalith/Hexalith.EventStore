namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Provider-owned purpose protection and non-rollback lifecycle evidence.</summary>
/// <param name="PolicyId">The exact accepted policy reference.</param>
/// <param name="Purpose">The purpose-separated protection scope.</param>
/// <param name="ExpiresAt">The derived irreversible expiry instant.</param>
/// <param name="LifecycleRevision">The monotonic anti-resurrection observation.</param>
/// <param name="EvidenceId">The opaque provider evidence reference.</param>
/// <param name="SourceExpiryEnforced">Whether events, snapshots and backups become unrecoverable.</param>
/// <param name="RestoreSafe">Whether rollback and restore cannot resurrect expired evidence.</param>
/// <param name="DerivedCopiesCovered">Whether projections and caches are covered.</param>
public sealed record IdentityHistoryCustodyEvidence(
    string PolicyId, string Purpose, DateTimeOffset ExpiresAt, long LifecycleRevision, string EvidenceId,
    bool SourceExpiryEnforced, bool RestoreSafe, bool DerivedCopiesCovered)
{
    /// <summary>Validates enforceable custody for the exact policy and immutable effective time.</summary>
    public bool Satisfies(IdentityHistoryPolicy policy, DateTimeOffset effectiveAt)
        => policy is not null && policy.IsValid && PolicyId == policy.PolicyId && Purpose == "party-actor-history-v1"
            && policy.DeriveExpiry(effectiveAt) is { } expectedExpiry && ExpiresAt == expectedExpiry && LifecycleRevision > 0
            && !string.IsNullOrWhiteSpace(EvidenceId) && SourceExpiryEnforced && RestoreSafe && DerivedCopiesCovered;
}
