namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Server-internal, opt-in ETag-conditional erase capability for the aggregate-specific projection
/// rebuild checkpoint row.
/// </summary>
/// <remarks>
/// <para>
/// This capability is deliberately kept off the released <see cref="IProjectionRebuildCheckpointStore"/>
/// contract (Story 1.14 owns rebuild-progress correctness and its member set must remain stable) and
/// internal to the Server package until an external consumer is proven. The same concrete
/// <see cref="ProjectionRebuildCheckpointStore"/> singleton implements both interfaces. A future
/// coordinator reads the ETag, conditionally erases, and performs its own read-back classification.
/// </para>
/// <para>
/// Both members erase ONLY the aggregate-specific
/// <c>(tenant, domain, projectionName, aggregateId)</c> row. They NEVER touch the operator-scope row
/// (<see cref="ProjectionRebuildCheckpointScope.AggregateId"/> = <see langword="null"/>, encoded with the
/// <c>*</c> suffix) or the active-rebuild index keys (<c>projection-rebuild-active-index:*</c> and
/// <c>projection-rebuild-active-index-pairs</c>). Both members fail closed by throwing
/// <see cref="ArgumentException"/> when the scope is operator-scoped, before any state access. This
/// capability does not change rebuild-progress semantics (Story 1.14 boundary).
/// </para>
/// </remarks>
internal interface IProjectionRebuildCheckpointEraser {
    /// <summary>
    /// Reads the current ETag of the aggregate-specific rebuild checkpoint row.
    /// </summary>
    /// <param name="scope">The aggregate-specific rebuild checkpoint scope.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A tuple whose <c>Present</c> is <see langword="true"/> and <c>Etag</c> is the current ETag when the
    /// aggregate-specific row exists; otherwise <c>Present</c> is <see langword="false"/> and <c>Etag</c> is
    /// the empty string.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="ProjectionRebuildCheckpointScope.AggregateId"/> is <see langword="null"/>
    /// (operator-scope). Fail closed before any state access so the operator-scope row and the active-rebuild
    /// index keys can never be read through this API.
    /// </exception>
    Task<(bool Present, string Etag)> TryReadAggregateCheckpointEtagAsync(
        ProjectionRebuildCheckpointScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts a FirstWrite-conditional delete of the aggregate-specific rebuild checkpoint row.
    /// </summary>
    /// <param name="scope">The aggregate-specific rebuild checkpoint scope.</param>
    /// <param name="etag">The expected ETag of the aggregate-specific row.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the row was erased or was already absent (idempotent);
    /// <see langword="false"/> when a present row carries a different ETag.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <see cref="ProjectionRebuildCheckpointScope.AggregateId"/> is <see langword="null"/>
    /// (operator-scope). Fail closed before any state access so the operator-scope row and the active-rebuild
    /// index keys can never be erased through this API.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="etag"/> is <see langword="null"/>.</exception>
    Task<bool> TryEraseAggregateCheckpointAsync(
        ProjectionRebuildCheckpointScope scope,
        string etag,
        CancellationToken cancellationToken = default);
}
