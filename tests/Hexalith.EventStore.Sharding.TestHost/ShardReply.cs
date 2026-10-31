using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>A complete synthetic range identity or an explicit unchanged-state denial.</summary>
/// <param name="Accepted">Whether the operation succeeded.</param>
/// <param name="Code">The bounded outcome code.</param>
/// <param name="State">The admitted state, if present.</param>
/// <param name="First">The first reserved counter, or zero for a nonreservation.</param>
/// <param name="Count">The number of reserved counters.</param>
public sealed record ShardReply(
    bool Accepted,
    string Code,
    ShardState? State = null,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long First = 0,
    int Count = 0);
