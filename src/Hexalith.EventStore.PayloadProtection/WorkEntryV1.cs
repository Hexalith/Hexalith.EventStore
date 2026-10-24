// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed WorkEntryV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="OperationId">The canonical operationId field.</param>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
internal sealed record WorkEntryV1(
    string OperationId,
    string ScopeDigest)
{
    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(WorkEntryV1);
}
