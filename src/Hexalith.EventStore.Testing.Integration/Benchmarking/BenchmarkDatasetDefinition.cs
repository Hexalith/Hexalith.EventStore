namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Describes a deterministic benchmark dataset to persist through the Dapr actor-state API.
/// </summary>
/// <param name="DatasetId">A stable, support-safe identifier for the dataset definition.</param>
/// <param name="Aggregates">The aggregate streams to create.</param>
public sealed record BenchmarkDatasetDefinition(
    string DatasetId,
    IReadOnlyList<BenchmarkAggregateDefinition> Aggregates);
