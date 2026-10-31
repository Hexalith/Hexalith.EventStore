using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>The single atomically saved synthetic admission and reservation record.</summary>
/// <param name="TenantId">The exact admitted tenant.</param>
/// <param name="Domain">The exact admitted domain.</param>
/// <param name="Generation">The admitted positive generation.</param>
/// <param name="Ceiling">The last reserved counter, including unused reservations.</param>
public sealed record ShardState(
    string TenantId,
    string Domain,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Generation,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Ceiling);
