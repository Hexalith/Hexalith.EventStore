using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Handles one canonical named projection route using an asynchronous, cancellation-aware persistence seam.
/// </summary>
public interface IAsyncDomainProjectionHandler {
    /// <summary>Gets the canonical kebab-case domain served by the handler.</summary>
    string Domain { get; }

    /// <summary>Gets the canonical kebab-case projection type served by the handler.</summary>
    string ProjectionType { get; }

    /// <summary>Projects the supplied event slice for a stable dispatch identity.</summary>
    /// <param name="request">The aggregate event slice to project.</param>
    /// <param name="dispatchId">The stable identity used for idempotent persistence.</param>
    /// <param name="cancellationToken">Propagates cancellation from the transport.</param>
    /// <returns>The durable outcome reported by the handler.</returns>
    Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken);
}
