namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Authenticated private revocation envelope; registrar has no human, public or signing authority.</summary>
/// <param name="Issuer">Approved revocation issuer.</param>
/// <param name="Audience">Committed protection registrar audience.</param>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="KeyFamily">Exact deletion signing key family.</param>
/// <param name="KeyVersion">Exact compromised version.</param>
/// <param name="RevocationRevision">Monotonic per-version revocation fact.</param>
/// <param name="TrustProfileRevision">Current independently authenticated profile revision.</param>
/// <param name="EventIdentity">Deterministic issuer event identity.</param>
/// <param name="SignatureDigest">Exact authenticated signature digest.</param>
public sealed record DeletionCapabilityRevocationEnvelope(string Issuer, string Audience, string TenantId, string KeyFamily, string KeyVersion, long RevocationRevision, long TrustProfileRevision, string EventIdentity, string SignatureDigest);
