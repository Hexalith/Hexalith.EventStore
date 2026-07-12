namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The structured outcome of a coordinated read-model batch execution.
/// </summary>
/// <remarks>
/// The status enum alone cannot express identity-versus-optimistic conflicts, cleanup-pending completion,
/// or the recovery reason without lossy interpretation, so those travel as explicit members. Expected
/// concurrency and durable-recovery outcomes are reported here; only validation, programming, and
/// configuration errors throw.
/// </remarks>
/// <param name="Status">The terminal classification.</param>
/// <param name="Fingerprint">The versioned canonical fingerprint of the batch that was evaluated.</param>
/// <param name="ConflictKind">The conflict cause when <see cref="Status"/> is <see cref="ReadModelBatchStatus.Conflict"/>.</param>
/// <param name="CleanupPending">
/// <see langword="true"/> when the durable values and completion receipt are proven but retryable
/// envelope/marker compaction has not finished. The batch is still complete.
/// </param>
/// <param name="RecoveryReason">An optional non-sensitive reason describing a reconciliation/recovery path.</param>
public sealed record ReadModelBatchResult(
    ReadModelBatchStatus Status,
    string Fingerprint,
    ReadModelBatchConflictKind ConflictKind,
    bool CleanupPending,
    string? RecoveryReason) {
    /// <summary>Gets a value indicating whether the batch reached a completed end state.</summary>
    public bool IsSuccess =>
        Status is ReadModelBatchStatus.Completed or ReadModelBatchStatus.AlreadyCompleted;

    /// <summary>Creates a completed-success result.</summary>
    /// <param name="fingerprint">The batch fingerprint.</param>
    /// <param name="cleanupPending">Whether retryable compaction remains.</param>
    /// <returns>A completed result.</returns>
    public static ReadModelBatchResult Completed(string fingerprint, bool cleanupPending = false) =>
        new(ReadModelBatchStatus.Completed, fingerprint, ReadModelBatchConflictKind.None, cleanupPending, null);

    /// <summary>Creates an idempotent already-completed result.</summary>
    /// <param name="fingerprint">The batch fingerprint.</param>
    /// <returns>An already-completed result.</returns>
    public static ReadModelBatchResult AlreadyCompleted(string fingerprint) =>
        new(ReadModelBatchStatus.AlreadyCompleted, fingerprint, ReadModelBatchConflictKind.None, false, null);

    /// <summary>Creates an identity-conflict result (same identity, different fingerprint).</summary>
    /// <param name="fingerprint">The rejected batch fingerprint.</param>
    /// <returns>An identity-conflict result.</returns>
    public static ReadModelBatchResult IdentityConflict(string fingerprint) =>
        new(ReadModelBatchStatus.Conflict, fingerprint, ReadModelBatchConflictKind.Identity, false, null);

    /// <summary>Creates an optimistic-conflict result (expected ETag mismatch, compensated).</summary>
    /// <param name="fingerprint">The batch fingerprint.</param>
    /// <param name="reason">An optional non-sensitive recovery reason.</param>
    /// <returns>An optimistic-conflict result.</returns>
    public static ReadModelBatchResult OptimisticConflict(string fingerprint, string? reason = null) =>
        new(ReadModelBatchStatus.Conflict, fingerprint, ReadModelBatchConflictKind.Optimistic, false, reason);

    /// <summary>Creates an incomplete result (retry with the same identity converges).</summary>
    /// <param name="fingerprint">The batch fingerprint.</param>
    /// <param name="reason">An optional non-sensitive recovery reason.</param>
    /// <returns>An incomplete result.</returns>
    public static ReadModelBatchResult Incomplete(string fingerprint, string? reason = null) =>
        new(ReadModelBatchStatus.Incomplete, fingerprint, ReadModelBatchConflictKind.None, false, reason);

    /// <summary>Creates an indeterminate result (a request may be durable but could not be proven).</summary>
    /// <param name="fingerprint">The batch fingerprint.</param>
    /// <param name="reason">An optional non-sensitive recovery reason.</param>
    /// <returns>An indeterminate result.</returns>
    public static ReadModelBatchResult Indeterminate(string fingerprint, string? reason = null) =>
        new(ReadModelBatchStatus.Indeterminate, fingerprint, ReadModelBatchConflictKind.None, false, reason);
}
