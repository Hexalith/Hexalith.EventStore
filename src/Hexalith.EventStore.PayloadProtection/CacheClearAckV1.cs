// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
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
