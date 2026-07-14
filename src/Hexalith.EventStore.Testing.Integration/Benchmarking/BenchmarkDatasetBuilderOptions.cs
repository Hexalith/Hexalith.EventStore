namespace Hexalith.EventStore.Testing.Integration.Benchmarking;

/// <summary>
/// Controls actor-state transaction bounds for benchmark dataset creation.
/// </summary>
public sealed class BenchmarkDatasetBuilderOptions {
    /// <summary>Gets or initializes the maximum number of operations in one actor-state transaction.</summary>
    public int MaxOperationsPerTransaction { get; init; } = 100;

    /// <summary>Gets or initializes the maximum serialized request-body bytes per transaction.</summary>
    public int MaxTransactionBytes { get; init; } = 1024 * 1024;

    /// <summary>Gets or initializes the maximum number of aggregate actors seeded concurrently.</summary>
    public int MaxConcurrentActors { get; init; } = 16;
}
