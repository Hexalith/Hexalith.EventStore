// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed LifecycleV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="TenantId">The canonical tenantId field.</param>
/// <param name="Domain">The canonical domain field.</param>
/// <param name="AggregateId">The canonical aggregateId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="State">The canonical state field.</param>
/// <param name="Epoch">The canonical epoch field.</param>
/// <param name="ReasonCode">The canonical reasonCode field.</param>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="ObservedAtUtc">The canonical observedAtUtc field.</param>
internal sealed record LifecycleV1(
    string TenantId,
    string Domain,
    string AggregateId,
    string ScopeDigest,
    string State,
    ulong Epoch,
    string ReasonCode,
    string OperationId,
    string ObservedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(LifecycleV1);
}
