// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed ReadLeaseV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="InstanceId">The canonical instanceId field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="IssuedAtUtc">The canonical issuedAtUtc field.</param>
/// <param name="ExpiresAtUtc">The canonical expiresAtUtc field.</param>
internal sealed record ReadLeaseV1(
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
    public override string ToString() => nameof(ReadLeaseV1);
}
