using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Opts a named projection handler into authoritative cleanup of a terminal stable dispatch.
/// </summary>
/// <remarks>
/// EventStore invokes this seam only for a route retained as terminal in its durable retry ledger. The handler
/// receives the same authoritative event prefix and stable dispatch identity as the failed delivery, so it can
/// converge partial work or compensate a still-pending marker without query-time repair.
/// </remarks>
public interface IAsyncDomainProjectionReconciliationHandler : IAsyncDomainProjectionHandler {
    /// <summary>Reconciles one terminal dispatch using its authoritative event prefix.</summary>
    /// <param name="request">The exact aggregate event prefix retained by EventStore.</param>
    /// <param name="dispatchId">The stable delivery identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed result only when no incomplete domain-owned marker remains.</returns>
    Task<DomainProjectionHandlerResult> ReconcileAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken);
}
