// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed CacheClearAckV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="InstanceId">The canonical instanceId field.</param>
/// <param name="AcknowledgedAtUtc">The canonical acknowledgedAtUtc field.</param>
internal sealed record CacheClearAckV1(
    string ScopeDigest,
    string OperationId,
    ulong LifecycleEpoch,
    string InstanceId,
    string AcknowledgedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(CacheClearAckV1);
}
