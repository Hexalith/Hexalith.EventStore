using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Records the non-payload details needed to validate or clean one seeded aggregate.
/// </summary>
/// <param name="Identity">The seeded aggregate identity.</param>
/// <param name="EventCount">The number of seeded events.</param>
/// <param name="HasSnapshot">Whether a snapshot was seeded.</param>
public sealed record BenchmarkAggregateReceipt(
    AggregateIdentity Identity,
    int EventCount,
    bool HasSnapshot);
