using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Server-internal, projection-scoped companion to the released <see cref="IProjectionCheckpointTracker"/>.
/// </summary>
/// <remarks>
/// The released <see cref="IProjectionCheckpointTracker"/> keys its delivery checkpoint by aggregate
/// identity only (<c>projection-checkpoints:{ActorId}</c>), so every projection derived from an aggregate
/// shares a single checkpoint. This companion keys the checkpoint by aggregate identity AND projection
/// name (<c>projection-checkpoints:{ActorId}:{projectionName}</c>) so each projection advances and drifts
/// independently. It is deliberately kept off the released contract (which must remain source/binary
/// compatible for third-party implementations) and internal to the Server package until an external
/// consumer is proven. The same concrete <see cref="ProjectionCheckpointTracker"/> singleton implements
/// both interfaces; migration from the legacy aggregate-wide key is performed lazily on first read and
/// the legacy key is never deleted.
/// </remarks>
internal interface IProjectionDeliveryCheckpointStore {
    /// <summary>
    /// Reads the last successfully delivered event sequence for a specific projection of an aggregate.
    /// </summary>
    /// <remarks>
    /// When the projection-scoped checkpoint is absent and no migration marker exists, the legacy
    /// aggregate-wide checkpoint is migrated in lazily (the projection-scoped key is seeded and a
    /// migration marker is persisted). Once the marker exists, an absent projection-scoped key is
    /// treated as an intentional erase and returns 0 rather than falling back to the legacy value.
    /// </remarks>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (the domain-service <c>ProjectionType</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The last delivered sequence for the projection, or 0 when no checkpoint exists.</returns>
    Task<long> ReadDeliveredSequenceAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a successful delivery checkpoint for a specific projection without lowering an existing higher sequence.
    /// </summary>
    /// <remarks>
    /// A successful save also finalizes migration (the migration marker is persisted) so that a later
    /// erase-then-read returns 0 instead of falling back to the legacy aggregate-wide value.
    /// </remarks>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (the domain-service <c>ProjectionType</c>).</param>
    /// <param name="deliveredSequence">The highest event sequence delivered successfully.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the checkpoint was saved; otherwise <see langword="false"/>.</returns>
    Task<bool> SaveDeliveredSequenceAsync(AggregateIdentity identity, string projectionName, long deliveredSequence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to erase the projection-scoped delivery checkpoint under optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// The migration marker is intentionally left in place so that subsequent reads return 0 rather than
    /// re-migrating the legacy aggregate-wide value.
    /// </remarks>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (the domain-service <c>ProjectionType</c>).</param>
    /// <param name="etag">The expected checkpoint ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the checkpoint was erased or already absent (idempotent);
    /// <see langword="false"/> when a present checkpoint has a different ETag.
    /// </returns>
    Task<bool> TryEraseAsync(AggregateIdentity identity, string projectionName, string etag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current ETag of the projection-scoped delivery checkpoint row for read-back classification.
    /// </summary>
    /// <remarks>
    /// This is a raw ETag read of the projection-scoped key only: it never triggers the lazy legacy
    /// migration performed by <see cref="ReadDeliveredSequenceAsync"/>, and it does not fall back to the
    /// legacy aggregate-wide checkpoint. The coordinated eraser passes the returned ETag straight to
    /// <see cref="TryEraseAsync"/> for a first-write-wins conditional erase.
    /// </remarks>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="projectionName">The projection name (the domain-service <c>ProjectionType</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <c>(true, etag)</c> when the projection-scoped checkpoint is present; <c>(false, "")</c> when absent.
    /// </returns>
    Task<(bool Present, string Etag)> TryReadDeliveryCheckpointEtagAsync(AggregateIdentity identity, string projectionName, CancellationToken cancellationToken = default);

    /// <summary>Reads the ETag of payload-free reconciliation work for coordinated erasure.</summary>
    Task<(bool Present, string Etag)> TryReadDeliveryReconciliationEtagAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default);

    /// <summary>Conditionally erases payload-free reconciliation work before the delivery row.</summary>
    Task<bool> TryEraseDeliveryReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        string etag,
        CancellationToken cancellationToken = default);
}
