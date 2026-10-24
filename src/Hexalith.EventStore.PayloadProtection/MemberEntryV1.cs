// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed MemberEntryV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="Ordinal">The canonical ordinal field.</param>
/// <param name="Generation">The canonical generation field.</param>
/// <param name="InstanceId">The canonical instanceId field.</param>
/// <param name="State">The canonical state field.</param>
internal sealed record MemberEntryV1(
    uint Ordinal,
    ulong Generation,
    string InstanceId,
    string State)
{
    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(MemberEntryV1);
}
