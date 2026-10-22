namespace Hexalith.EventStore.ServiceDefaults.Authentication;

/// <summary>Authenticated human authority bound to one domain command invocation.</summary>
/// <param name="Version">Receipt format version.</param>
/// <param name="Issuer">The issuing gateway.</param>
/// <param name="Audience">The receiving domain service.</param>
/// <param name="IssuedAtUnixSeconds">UTC issuance time.</param>
/// <param name="ExpiresAtUnixSeconds">UTC expiration time.</param>
/// <param name="InvocationId">Unique identifier for this invocation.</param>
/// <param name="Tenant">Verified human tenant.</param>
/// <param name="Subject">Verified human subject.</param>
/// <param name="Permissions">Verified human permissions.</param>
/// <param name="CommandDigest">SHA-256 digest of the canonical command.</param>
public sealed record HumanAuthorityReceiptClaims(
    int Version,
    string Issuer,
    string Audience,
    long IssuedAtUnixSeconds,
    long ExpiresAtUnixSeconds,
    string InvocationId,
    string Tenant,
    string Subject,
    string[] Permissions,
    string CommandDigest);
