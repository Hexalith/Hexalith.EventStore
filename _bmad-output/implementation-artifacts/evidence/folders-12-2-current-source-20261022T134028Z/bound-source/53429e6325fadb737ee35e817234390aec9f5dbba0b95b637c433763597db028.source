using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — stable idempotency key for a crypto-shredding workflow request. Two requests with
/// identical identities are the same workflow; the second one returns the existing
/// audit/status without creating a duplicate record. Equality is value-based across every required
/// field. Raw key aliases NEVER appear: <see cref="KeyAliasFingerprint"/> stores a SHA-256 hex
/// prefix only when <see cref="KeyReferencePolicy"/> permits.
/// </summary>
/// <param name="WorkflowId">Caller-supplied stable ULID identifying the workflow request.</param>
/// <param name="TenantId">Multi-tenant scope.</param>
/// <param name="Domain">Domain scope.</param>
/// <param name="Scope">The scope of the workflow request.</param>
/// <param name="AggregateId">Aggregate identifier (required when scope is <c>Aggregate</c>/<c>Stream</c>/<c>Range</c>).</param>
/// <param name="FromSequence">Inclusive lower bound of the affected sequence range (required when scope is <c>Range</c>).</param>
/// <param name="ToSequence">Inclusive upper bound of the affected sequence range. <see cref="long.MaxValue"/> means open-ended.</param>
/// <param name="KeyReferencePolicy">The policy controlling whether a key reference is stored.</param>
/// <param name="KeyAliasFingerprint">Optional SHA-256 hex prefix of the key alias (16 hex chars).</param>
public sealed record CryptoShreddingWorkflowIdentity(
    string WorkflowId,
    string TenantId,
    string Domain,
    CryptoShreddingWorkflowScope Scope,
    string? AggregateId,
    long? FromSequence,
    long? ToSequence,
    KeyReferencePolicy KeyReferencePolicy,
    string? KeyAliasFingerprint) {
    /// <summary>Maximum length of the workflow ULID identifier.</summary>
    public const int MaxWorkflowIdLength = 26;

    /// <summary>Length of the SHA-256 hex prefix used for key alias fingerprinting.</summary>
    public const int KeyAliasFingerprintLength = 16;

    /// <summary>Validates the structural invariants required by <see cref="CryptoShreddingWorkflowIdentity"/>.</summary>
    /// <param name="rejectionReason">A short human-readable rejection reason when validation fails.</param>
    /// <returns><see langword="true"/> when the identity is structurally valid.</returns>
    public bool TryValidate(out string? rejectionReason) {
        if (!Enum.IsDefined(Scope)) {
            rejectionReason = "Scope is not a supported crypto-shredding workflow scope.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(WorkflowId) || WorkflowId.Length > MaxWorkflowIdLength) {
            rejectionReason = "WorkflowId is required and bounded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(TenantId)) {
            rejectionReason = "TenantId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Domain)) {
            rejectionReason = "Domain is required.";
            return false;
        }

        if (Scope is CryptoShreddingWorkflowScope.Aggregate or CryptoShreddingWorkflowScope.Stream or CryptoShreddingWorkflowScope.Range
            && string.IsNullOrWhiteSpace(AggregateId)) {
            rejectionReason = "AggregateId is required for Aggregate/Stream/Range scopes.";
            return false;
        }

        if (Scope == CryptoShreddingWorkflowScope.Range) {
            if (FromSequence is null || ToSequence is null) {
                rejectionReason = "Range scope requires FromSequence and ToSequence.";
                return false;
            }

            if (FromSequence < 0 || ToSequence < FromSequence) {
                rejectionReason = "Range scope sequence bounds are invalid.";
                return false;
            }
        }

        if (KeyReferencePolicy != KeyReferencePolicy.NoKeyReference) {
            if (string.IsNullOrWhiteSpace(KeyAliasFingerprint) || KeyAliasFingerprint.Length != KeyAliasFingerprintLength) {
                rejectionReason = "KeyAliasFingerprint must be a 16-character hex string when policy allows a reference.";
                return false;
            }

            for (int i = 0; i < KeyAliasFingerprint.Length; i++) {
                char c = KeyAliasFingerprint[i];
                bool isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
                if (!isHex) {
                    rejectionReason = "KeyAliasFingerprint must be lowercase hex.";
                    return false;
                }
            }
        }
        else if (!string.IsNullOrEmpty(KeyAliasFingerprint)) {
            rejectionReason = "KeyAliasFingerprint must be empty when policy is NoKeyReference.";
            return false;
        }

        rejectionReason = null;
        return true;
    }

    /// <summary>
    /// Computes the stable idempotency scope key. The caller-supplied <see cref="WorkflowId"/> is
    /// intentionally excluded so repeated requests over the same protected-data scope resolve to
    /// the same workflow record.
    /// </summary>
    /// <returns>A SHA-256 hex key derived from tenant/domain/scope/range/key-reference metadata.</returns>
    public string ComputeScopeKey() {
        string material = string.Join(
            "|",
            TenantId,
            Domain,
            Scope.ToString(),
            AggregateId ?? string.Empty,
            FromSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ToSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            KeyReferencePolicy.ToString(),
            KeyAliasFingerprint ?? string.Empty);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        var builder = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++) {
            _ = builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Computes a SHA-256 hex prefix fingerprint of the supplied key alias. NEVER persist the raw
    /// alias; persist the fingerprint instead.
    /// </summary>
    /// <param name="keyAlias">The key alias.</param>
    /// <returns>A lowercase 16-character hex prefix of the SHA-256 hash.</returns>
    public static string ComputeKeyAliasFingerprint(string keyAlias) {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyAlias);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyAlias));
        var builder = new StringBuilder(KeyAliasFingerprintLength);
        for (int i = 0; i < KeyAliasFingerprintLength / 2; i++) {
            _ = builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
