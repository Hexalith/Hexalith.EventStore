// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed InvalidationDrainV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantDomainDigest">The canonical tenantDomainDigest field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="LifecycleEpoch">The canonical lifecycleEpoch field.</param>
/// <param name="MemberCutoff">The canonical memberCutoff field.</param>
internal sealed record InvalidationDrainV1(
    string TenantDomainDigest,
    string ScopeDigest,
    string OperationId,
    ulong LifecycleEpoch,
    uint MemberCutoff)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(InvalidationDrainV1);
}
