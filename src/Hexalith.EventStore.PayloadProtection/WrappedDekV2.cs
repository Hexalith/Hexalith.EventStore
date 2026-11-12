// Approved Dapr Kubernetes cryptography supplement: 3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd; section 5.
// Profile authority: 5d11b464555ac1f40fde3eadfd03c2a3f984f0770d594b30062a4cc641e34204; WrappedDekV1 bytes and meaning are unchanged.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed WrappedDekV2 durable schema: the 19 V1 fields at the same state key, readable only under the Dapr Kubernetes profile.</summary>
/// <remarks>It derives from <see cref="WrappedDekV1"/> only so the unchanged lifecycle callers keep one field shape; record transitions preserve the V2 type.
/// The state codec dispatches on schemaVersion and never converts or restamps a V1 record into this schema.</remarks>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="AggregateId">The canonical aggregateId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="State">The canonical state field.</param>
/// <param name="BackendId">The exact backend identity, dapr-crypto-kubernetes-secrets-v1.</param>
/// <param name="ProviderKeyVersionId">The trusted namespace/secretName/dataKey key address.</param>
/// <param name="WrapAlgorithmId">The exact algorithm identity, dapr-enc-v1-rsa-oaep-256-aes-gcm.</param>
/// <param name="WrappedDek">The canonical base64url Dapr encryption-scheme v1 envelope.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="FenceApprovalEpoch">The canonical fenceApprovalEpoch field.</param>
/// <param name="HighestWrittenEpoch">The canonical highestWrittenEpoch field.</param>
/// <param name="CreatingOperationId">The canonical creatingOperationId field.</param>
/// <param name="CreatedAtUtc">The canonical createdAtUtc field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
/// <param name="CanonicalFieldsSha256">The canonical field hash under magic HXKF 02 11 00 00.</param>
/// <param name="WrappedDekSha256">The canonical wrappedDekSha256 field.</param>
internal sealed record WrappedDekV2(
    string TenantId,
    string Domain,
    string AggregateId,
    string ScopeDigest,
    string KeyRef,
    uint DekVersion,
    string State,
    string BackendId,
    string ProviderKeyVersionId,
    string WrapAlgorithmId,
    string WrappedDek,
    ulong LifecycleEpoch,
    ulong FenceApprovalEpoch,
    ulong HighestWrittenEpoch,
    string CreatingOperationId,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    string CanonicalFieldsSha256,
    string WrappedDekSha256)
    : WrappedDekV1(TenantId, Domain, AggregateId, ScopeDigest, KeyRef, DekVersion, State, BackendId, ProviderKeyVersionId,
        WrapAlgorithmId, WrappedDek, LifecycleEpoch, FenceApprovalEpoch, HighestWrittenEpoch, CreatingOperationId, CreatedAtUtc,
        UpdatedAtUtc, CanonicalFieldsSha256, WrappedDekSha256)
{
    /// <summary>Gets the only backend identity this schema admits.</summary>
    internal const string ClosedBackendId = "dapr-crypto-kubernetes-secrets-v1";
    /// <summary>Gets the only wrap algorithm identity this schema admits.</summary>
    internal const string ClosedWrapAlgorithmId = "dapr-enc-v1-rsa-oaep-256-aes-gcm";
    /// <summary>Gets the decoded envelope bound; every other backend keeps the 512-byte V1 bound.</summary>
    internal const int MaximumEnvelopeBytes = 2048;

    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public override int SchemaVersion { get; init; } = 2;

    /// <summary>Gets the exact ASCII prefix of a complete Dapr encryption-scheme v1 envelope.</summary>
    internal static ReadOnlySpan<byte> SchemePrefix => "dapr.io/enc/v1\n"u8;

    /// <summary>Checks the closed decoded envelope bound and scheme prefix without reframing the bytes.</summary>
    internal static bool IsEnvelope(ReadOnlySpan<byte> envelope)
        => envelope.Length is >= 1 and <= MaximumEnvelopeBytes && envelope.StartsWith(SchemePrefix);

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(WrappedDekV2);
}
