namespace Hexalith.EventStore.Server.Security;

/// <summary>
/// Independently published deletion-capability verifier. A fixture copy is component evidence and is not an installed anchor.
/// </summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="KeyFamily">Exact deletion signing purpose.</param>
/// <param name="CapabilityKeyVersion">Approved version.</param>
/// <param name="Issuer">Approved issuer.</param>
/// <param name="Audience">Approved protection owner.</param>
/// <param name="PublicAnchorId">Published anchor identity.</param>
/// <param name="PublicAnchorVersion">Published anchor version.</param>
/// <param name="SubjectPublicKeyInfo">Published P-256 public key bytes.</param>
/// <param name="TrustProfileRevision">Independently recorded profile revision.</param>
/// <param name="IsRevoked">Independent emergency revocation. A revoked entry cannot authorize consumption.</param>
public sealed record PublishedDeletionCapabilityTrust(
    string TenantId,
    string KeyFamily,
    string CapabilityKeyVersion,
    string Issuer,
    string Audience,
    string PublicAnchorId,
    string PublicAnchorVersion,
    byte[] SubjectPublicKeyInfo,
    long TrustProfileRevision,
    bool IsRevoked);
