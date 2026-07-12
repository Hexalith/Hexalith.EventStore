using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Server-internal, opt-in ETag-conditional erase capability for the projection delivery checkpoint.
/// </summary>
/// <remarks>
/// This capability is deliberately kept off the released <see cref="IProjectionCheckpointTracker"/>
/// contract (which must remain source/binary compatible for third-party implementations) and internal to
/// the Server package until an external consumer is proven. The coordinated projection eraser resolves it
/// before any mutation and reports <c>Unsupported</c> when it has not opted in. The same concrete
/// <see cref="ProjectionCheckpointTracker"/> singleton implements both interfaces.
/// </remarks>
internal interface IProjectionCheckpointEraser {
    /// <summary>
    /// Attempts to erase the aggregate delivery checkpoint under optimistic concurrency.
    /// </summary>
    /// <param name="identity">The aggregate identity.</param>
    /// <param name="etag">The expected checkpoint ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the checkpoint was erased or already absent (idempotent);
    /// <see langword="false"/> when a present checkpoint has a different ETag.
    /// </returns>
    Task<bool> TryEraseAsync(
        AggregateIdentity identity,
        string etag,
        CancellationToken cancellationToken = default);
}
