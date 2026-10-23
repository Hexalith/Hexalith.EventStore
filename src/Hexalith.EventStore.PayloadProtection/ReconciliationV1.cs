// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed ReconciliationV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="Kind">The canonical kind field.</param>
/// <param name="NotBeforeUtc">The canonical notBeforeUtc field.</param>
/// <param name="Attempt">The canonical attempt field.</param>
/// <param name="Status">The canonical status field.</param>
/// <param name="ReasonCode">The canonical reasonCode field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
internal sealed record ReconciliationV1(
    string OperationId,
    string ScopeDigest,
    string KeyRef,
    uint DekVersion,
    string Kind,
    string NotBeforeUtc,
    uint Attempt,
    string Status,
    string ReasonCode,
    string UpdatedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(ReconciliationV1);
}
