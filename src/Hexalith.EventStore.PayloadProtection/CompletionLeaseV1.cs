// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed CompletionLeaseV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="InstanceId">The canonical instanceId field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="IssuedAtUtc">The canonical issuedAtUtc field.</param>
/// <param name="ExpiresAtUtc">The canonical expiresAtUtc field.</param>
internal sealed record CompletionLeaseV1(
    string OperationId,
    string ScopeDigest,
    string KeyRef,
    uint DekVersion,
    string InstanceId,
    ulong LifecycleEpoch,
    string IssuedAtUtc,
    string ExpiresAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(CompletionLeaseV1);
}
