namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Opt-in, same-store coordinated batch capability for persisted read models.
/// </summary>
/// <remarks>
/// <para>
/// This is an <b>additive</b> companion to <see cref="IReadModelStore"/>: it lets a projection author
/// persist several heterogeneous typed writes and deletes (for example a detail model and its index
/// entry) as one coordinated unit within a single configured DAPR state-store component, so a reader
/// never observes an updated detail model paired with a missing or stale index entry.
/// </para>
/// <para>
/// The execution is immediate and asynchronous: one immutable <see cref="ReadModelBatch"/> in, one
/// structured <see cref="ReadModelBatchResult"/> out. There is no mutable builder, buffered flush, or
/// fire-and-forget work. Expected concurrency and durable-recovery outcomes are reported through the
/// result status; only validation, programming, and configuration errors throw.
/// </para>
/// <para>
/// The same concrete instance also implements <see cref="IReadModelStore"/>; the platform registers one
/// singleton behind both interfaces (see <c>AddEventStoreReadModelStore</c>).
/// </para>
/// </remarks>
public interface IReadModelBatchStore {
    /// <summary>
    /// Executes a coordinated read-model batch against its configured store component.
    /// </summary>
    /// <param name="batch">The immutable batch manifest (scope, ordered operations, concurrency policy).</param>
    /// <param name="cancellationToken">
    /// A cancellation token. Cancellation requested before any state access throws
    /// <see cref="OperationCanceledException"/>; cancellation observed after a request may have reached
    /// the store triggers bounded durable reconciliation rather than being treated as a rollback.
    /// </param>
    /// <returns>
    /// A structured <see cref="ReadModelBatchResult"/> distinguishing completed success, idempotent
    /// already-completed success, conflict, incomplete, and indeterminate outcomes.
    /// </returns>
    Task<ReadModelBatchResult> ExecuteAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default);
}
