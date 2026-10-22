namespace Hexalith.EventStore.Contracts.Security;

/// <summary>The register's closed non-expiring V1 destruction-capability payload. It contains no successful committed issue revision or renewal field.</summary>
/// <param name="Issuer">Exact authenticated signing issuer.</param>
/// <param name="Audience">Committed protection-owner audience.</param>
/// <param name="TenantId">Exact tenant and per-tenant key family.</param>
/// <param name="DeletionRequestId">Original deletion request.</param>
/// <param name="DestructionSealId">Once-assigned stable seal identity.</param>
/// <param name="BatchKind">Exact authorized batch kind; the byte codec does not authorize domain meanings.</param>
/// <param name="BatchOrdinal">Original nonnegative batch ordinal.</param>
/// <param name="BatchId">Immutable exact batch identity.</param>
/// <param name="ManifestDigest">Exact sorted target-manifest digest.</param>
/// <param name="GuardStreamId">Exact recorded guard owner.</param>
/// <param name="IntendedIssuedGuardRevision">Expected issue compare revision, never the actual successful revision.</param>
/// <param name="AttestationOrdinal">Original or expected successor active attestation ordinal.</param>
/// <param name="SigningAttemptOrdinal">Monotonic pre-issue signing attempt.</param>
/// <param name="CapabilityKeyVersion">Exact per-tenant deletion-capability key version.</param>
public sealed record DeletionBatchCapabilityV1(string Issuer, string Audience, string TenantId, string DeletionRequestId,
    string DestructionSealId, string BatchKind, long BatchOrdinal, string BatchId, string ManifestDigest, string GuardStreamId,
    long IntendedIssuedGuardRevision, long AttestationOrdinal, long SigningAttemptOrdinal, string CapabilityKeyVersion);
