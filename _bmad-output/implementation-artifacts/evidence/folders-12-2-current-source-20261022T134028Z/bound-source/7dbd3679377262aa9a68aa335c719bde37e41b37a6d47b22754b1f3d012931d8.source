namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Persisted exact revision-bound authorization. Any intervening mutation requires a stale result and fresh authorization; an old read never seals or dispatches.</summary>
/// <param name="AuthorizationId">Exact AuthorizationId.</param>
/// <param name="Effect">Exact Effect.</param>
/// <param name="RequestId">Exact RequestId.</param>
/// <param name="BatchId">Exact BatchId.</param>
/// <param name="GuardRevision">Exact GuardRevision.</param>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="PredecessorBindingId">Exact PredecessorBindingId.</param>
/// <param name="ManifestDigest">Exact ManifestDigest.</param>
/// <param name="DispositionVersion">Exact DispositionVersion.</param>
/// <param name="AuthorityReceiptId">Exact AuthorityReceiptId.</param>
/// <param name="EffectBasisDigest">Exact immutable scope, owner obligations, candidate cut/token or batch/attestation basis.</param>
public sealed record GovernanceAuthorization(string AuthorizationId, GovernanceGuardOperation Effect, string RequestId, string BatchId, long GuardRevision, long Ordinal, string PredecessorBindingId, string ManifestDigest, string DispositionVersion, string AuthorityReceiptId, string EffectBasisDigest);
