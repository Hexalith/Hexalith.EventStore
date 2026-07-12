namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The execution profile configured for a state-store component used by read-model batches.
/// </summary>
/// <remarks>
/// Qualification is an operator-owned semantic promise, not an inference from DAPR component metadata or a
/// void transaction response. A store defaults to <see cref="Resumable"/> and must only be configured as
/// <see cref="TransactionQualified"/> when a live conditional-write probe proves all-or-nothing behavior
/// for the exact deployed runtime/component/backend combination.
/// </remarks>
public enum ReadModelBatchStoreProfile {
    /// <summary>
    /// The default. Uses the marker-gated resumable protocol: previous complete values remain visible
    /// until a durable commit marker, and interrupted batches converge on retry with the same identity.
    /// </summary>
    Resumable,

    /// <summary>
    /// Operator-qualified as transaction-safe. Uses a single ordered DAPR state transaction plus terminal
    /// completion evidence, verified by read-back.
    /// </summary>
    TransactionQualified,
}
