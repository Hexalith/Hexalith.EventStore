// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed OperationV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="Operation">The canonical operation field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="Phase">The canonical phase field.</param>
/// <param name="Attempt">The canonical attempt field.</param>
/// <param name="CreatedAtUtc">The canonical createdAtUtc field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
/// <param name="Result">The canonical result field.</param>
/// <param name="ReasonCode">The canonical reasonCode field.</param>
internal sealed record OperationV1(
    string OperationId,
    string Operation,
    string ScopeDigest,
    string KeyRef,
    uint DekVersion,
    string Phase,
    uint Attempt,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    string Result,
    string ReasonCode)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(OperationV1);
}
