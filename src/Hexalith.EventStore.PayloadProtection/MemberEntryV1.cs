// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
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
