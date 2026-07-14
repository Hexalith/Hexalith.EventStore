using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Describes one aggregate stream in a benchmark dataset.
/// </summary>
/// <param name="Identity">The production aggregate identity.</param>
/// <param name="AggregateType">The aggregate type stored on every event envelope.</param>
/// <param name="Events">The ordered event definitions. Their list position determines sequence number.</param>
/// <param name="Snapshot">An optional domain-supplied snapshot.</param>
public sealed record BenchmarkAggregateDefinition(
    AggregateIdentity Identity,
    string AggregateType,
    IReadOnlyList<BenchmarkEventDefinition> Events,
    BenchmarkSnapshotDefinition? Snapshot = null);
