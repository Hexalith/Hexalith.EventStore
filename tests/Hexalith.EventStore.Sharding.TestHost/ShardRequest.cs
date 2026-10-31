using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>A synthetic test-only operation, with JSON-visible members.</summary>
/// <param name="Operation">Admit, Reserve, Inspect, or Seed.</param>
/// <param name="TenantId">The exact tenant.</param>
/// <param name="Domain">The exact domain.</param>
/// <param name="Generation">The explicitly requested positive generation.</param>
/// <param name="Count">The reservation size.</param>
/// <param name="SeedCeiling">The internal test seed, allowed only at an admitted zero ceiling.</param>
/// <param name="OperationId">An optional observation identity; it never deduplicates execution.</param>
public sealed record ShardRequest(
    string Operation,
    string TenantId,
    string Domain,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long Generation,
    int Count = 0,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] long SeedCeiling = 0,
    string? OperationId = null);
