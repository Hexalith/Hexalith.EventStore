using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Opts a tenant/domain shared projection into idempotent work that runs only after its rebuilt
/// canonical view is durably committed and verified.
/// </summary>
/// <remarks>
/// EventStore durably retains the handler-provided completion state with the rebuild session and
/// invokes this seam again when a prior completion attempt was retryable or indeterminate. The
/// implementation must therefore be idempotent and must not assume exactly-once invocation.
/// Returning anything other than <see cref="ProjectionDispatchStatus.Completed"/> or
/// <see cref="ProjectionDispatchStatus.AlreadyCompleted"/> keeps the rebuild incomplete without
/// rolling back the already-committed canonical read model.
/// </remarks>
public interface IAsyncDomainSharedProjectionRebuildCompletionHandler : IAsyncDomainSharedProjectionRebuildHandler {
    /// <summary>Completes domain-owned external reconciliation for one committed shared rebuild.</summary>
    /// <param name="identity">The stable shared rebuild identity.</param>
    /// <param name="candidate">The immutable rebuilt candidate that was committed.</param>
    /// <param name="completionState">Opaque handler-owned state captured before commit.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed result only when the post-commit work has converged.</returns>
    Task<DomainProjectionHandlerResult> CompleteRebuildAsync(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildCandidate candidate,
        ReadOnlyMemory<byte> completionState,
        CancellationToken cancellationToken);
}
