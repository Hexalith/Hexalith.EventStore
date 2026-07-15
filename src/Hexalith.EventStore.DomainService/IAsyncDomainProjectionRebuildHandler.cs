using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Opts a named projection handler into side-effect-free rebuild planning for coordinated promotion.
/// </summary>
public interface IAsyncDomainProjectionRebuildHandler : IAsyncDomainProjectionHandler {
    /// <summary>Gets the handler's explicit rebuild input semantics.</summary>
    DomainProjectionRebuildSemantics RebuildSemantics { get; }

    /// <summary>
    /// Creates an operation-scoped candidate plan without changing live read-model state.
    /// </summary>
    /// <param name="request">The complete rebuild input required by <see cref="RebuildSemantics"/>.</param>
    /// <param name="operationId">The stable rebuild operation identity used by coordinated persistence.</param>
    /// <param name="cancellationToken">Propagates request cancellation.</param>
    /// <returns>The candidate operations to include in the operation-wide batch.</returns>
    Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(
        ProjectionRequest request,
        string operationId,
        CancellationToken cancellationToken);
}
