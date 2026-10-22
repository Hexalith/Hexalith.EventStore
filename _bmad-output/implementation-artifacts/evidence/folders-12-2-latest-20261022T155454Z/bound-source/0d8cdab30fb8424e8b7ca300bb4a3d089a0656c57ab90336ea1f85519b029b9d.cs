using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A minimal persistence-capable named projection used to prove scoped discovery.</summary>
public sealed class WidgetAsyncProjectionHandler : IAsyncDomainProjectionHandler {
    /// <inheritdoc/>
    public string Domain => "widget";

    /// <inheritdoc/>
    public string ProjectionType => "widget-detail";

    /// <inheritdoc/>
    public Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DomainProjectionHandlerResult.Completed());
    }
}
