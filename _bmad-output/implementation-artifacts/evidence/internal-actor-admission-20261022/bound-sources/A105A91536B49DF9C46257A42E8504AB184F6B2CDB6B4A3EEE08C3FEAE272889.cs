namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Gateway-owned verified provenance, bound cryptographically to one operation.</summary>
/// <param name="Scope">The immutable admitted scope.</param>
/// <param name="SourceId">The configured trusted workload.</param>
/// <param name="OperatorActorId">The verified opaque operator attribution.</param>
/// <param name="TargetActorId">The verified opaque target actor, when applicable.</param>
/// <param name="ActorRevision">The target registry revision.</param>
/// <param name="ActorActive">Whether the target was active at this observation.</param>
/// <param name="IssuedAt">The proof issue instant.</param>
/// <param name="ExpiresAt">The exclusive proof expiry instant.</param>
/// <param name="AuthorityRevision">The configured trust-policy revision.</param>
public sealed record IdentityAdmissionEvidence(IdentityAdmissionScope Scope, string SourceId,
    string? OperatorActorId, string? TargetActorId, long ActorRevision, bool ActorActive,
    DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, long AuthorityRevision);
