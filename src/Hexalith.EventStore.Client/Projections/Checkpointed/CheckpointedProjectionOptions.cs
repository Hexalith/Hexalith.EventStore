namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Tuning for checkpointed projection writes and fenced recovery.
/// </summary>
public sealed class CheckpointedProjectionOptions {
    /// <summary>Gets or sets the identity this process uses for recovery leases.</summary>
    public string WorkerId { get; set; } = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    /// <summary>Gets or sets how long a recovery lease fences other workers.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the total compare-and-swap attempts per operation, counting the first one
    /// (defaults to <see cref="ReadModelWritePolicy.DefaultMaxAttempts"/>).
    /// </summary>
    public int MaxAttempts { get; set; } = ReadModelWritePolicy.DefaultMaxAttempts;

    /// <summary>Gets or sets the page size used for authoritative range reads.</summary>
    public int HistoryPageSize { get; set; } = 100;

    /// <summary>Gets or sets the most authoritative pages read for one range before failing as unavailable.</summary>
    public int MaxHistoryPages { get; set; } = 1000;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <exception cref="ArgumentException">An option is outside its supported range.</exception>
    public void Validate() {
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkerId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(HistoryPageSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxHistoryPages, 1);
    }
}
