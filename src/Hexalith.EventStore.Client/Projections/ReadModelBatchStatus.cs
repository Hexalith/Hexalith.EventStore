namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The terminal classification of a coordinated read-model batch execution.
/// </summary>
/// <remarks>
/// A durable prefix, staging write, deferred flush, or cleanup progress never reports
/// <see cref="Completed"/>. Only a proven end state (every operation and the completion receipt durable)
/// is <see cref="Completed"/>. Ambiguous outcomes are <see cref="Indeterminate"/>, never optimistically
/// upgraded to success.
/// </remarks>
public enum ReadModelBatchStatus {
    /// <summary>Every operation and the terminal completion receipt were durably proven in this call.</summary>
    Completed,

    /// <summary>
    /// The same identity and fingerprint already have a terminal completion receipt; the batch was not
    /// reapplied.
    /// </summary>
    AlreadyCompleted,

    /// <summary>
    /// The batch did not complete because of an identity conflict (same identity, different fingerprint)
    /// or an optimistic-concurrency conflict on an expected ETag. No partial success is reported.
    /// </summary>
    Conflict,

    /// <summary>
    /// Durable completion was not proven and the same identity can resume without losing already-durable
    /// state.
    /// </summary>
    Incomplete,

    /// <summary>
    /// A request may have reached the store but durable completion or conflict could not be proven within
    /// the bounded reconciliation window.
    /// </summary>
    Indeterminate,
}
