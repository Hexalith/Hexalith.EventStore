namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Reports a successfully written and read-back-validated benchmark dataset.
/// </summary>
/// <param name="DatasetId">The caller-supplied dataset identifier.</param>
/// <param name="Fingerprint">The deterministic SHA-256 dataset fingerprint.</param>
/// <param name="AggregateCount">The number of seeded aggregate streams.</param>
/// <param name="EventCount">The total number of seeded events.</param>
/// <param name="SnapshotCount">The number of seeded snapshots.</param>
/// <param name="FirstGlobalPosition">The first reserved global position.</param>
/// <param name="LastGlobalPosition">The last reserved global position.</param>
/// <param name="ValidationDuration">Time spent validating inputs and proving every target stream was absent.</param>
/// <param name="AllocationDuration">Time spent reserving the single global-position range.</param>
/// <param name="WriteDuration">Time spent writing bounded actor-state transactions.</param>
/// <param name="ReadBackValidationDuration">Time spent validating persisted metadata and boundary events.</param>
/// <param name="TotalDuration">Total dataset-builder duration.</param>
/// <param name="Aggregates">Non-payload per-aggregate cleanup details.</param>
public sealed record BenchmarkDatasetReceipt(
    string DatasetId,
    string Fingerprint,
    int AggregateCount,
    int EventCount,
    int SnapshotCount,
    long FirstGlobalPosition,
    long LastGlobalPosition,
    TimeSpan ValidationDuration,
    TimeSpan AllocationDuration,
    TimeSpan WriteDuration,
    TimeSpan ReadBackValidationDuration,
    TimeSpan TotalDuration,
    IReadOnlyList<BenchmarkAggregateReceipt> Aggregates);
