// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed WrappedDekV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="AggregateId">The canonical aggregateId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="State">The canonical state field.</param>
/// <param name="BackendId">The canonical backendId field.</param>
/// <param name="ProviderKeyVersionId">The canonical providerKeyVersionId field.</param>
/// <param name="WrapAlgorithmId">The canonical wrapAlgorithmId field.</param>
/// <param name="WrappedDek">The canonical wrappedDek field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="FenceApprovalEpoch">The canonical fenceApprovalEpoch field.</param>
/// <param name="HighestWrittenEpoch">The canonical highestWrittenEpoch field.</param>
/// <param name="CreatingOperationId">The canonical creatingOperationId field.</param>
/// <param name="CreatedAtUtc">The canonical createdAtUtc field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
/// <param name="CanonicalFieldsSha256">The canonical canonicalFieldsSha256 field.</param>
/// <param name="WrappedDekSha256">The canonical wrappedDekSha256 field.</param>
internal sealed record WrappedDekV1(
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
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(WrappedDekV1);
}
