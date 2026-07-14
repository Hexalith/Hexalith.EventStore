using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Supplies an opaque, deterministic snapshot for a benchmark aggregate.
/// </summary>
/// <param name="SequenceNumber">The event sequence represented by the snapshot.</param>
/// <param name="State">The domain-owned snapshot state serialized as JSON.</param>
/// <param name="CreatedAt">The deterministic snapshot creation timestamp.</param>
/// <param name="ProtectionMetadata">The protection metadata matching <paramref name="State"/>.</param>
public sealed record BenchmarkSnapshotDefinition(
    long SequenceNumber,
    JsonElement State,
    DateTimeOffset CreatedAt,
    EventStorePayloadProtectionMetadata ProtectionMetadata);
