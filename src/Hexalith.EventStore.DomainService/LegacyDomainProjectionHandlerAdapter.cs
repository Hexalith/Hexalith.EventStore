using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Adapts one explicitly selected legacy full-replay handler to one named asynchronous projection route.
/// </summary>
public sealed class LegacyDomainProjectionHandlerAdapter : IAsyncDomainProjectionHandler {
    private readonly IDomainProjectionHandler _handler;

    /// <summary>Initializes a new instance of the <see cref="LegacyDomainProjectionHandlerAdapter"/> class.</summary>
    /// <param name="handler">The legacy handler selected for this route.</param>
    /// <param name="domain">The exact canonical domain route.</param>
    /// <param name="projectionType">The exact canonical projection route.</param>
    public LegacyDomainProjectionHandlerAdapter(
        IDomainProjectionHandler handler,
        string domain,
        string projectionType) {
        ArgumentNullException.ThrowIfNull(handler);
        NamingConventionEngine.ValidateKebabCase(domain, nameof(domain));
        NamingConventionEngine.ValidateKebabCase(projectionType, nameof(projectionType));

        if (!string.Equals(handler.Domain, domain, StringComparison.Ordinal)) {
            throw new ArgumentException(
                $"Legacy handler domain '{handler.Domain}' does not match explicit adapter domain '{domain}'.",
                nameof(domain));
        }

        _handler = handler;
        Domain = domain;
        ProjectionType = projectionType;
    }

    /// <inheritdoc/>
    public string Domain { get; }

    /// <inheritdoc/>
    public string ProjectionType { get; }

    /// <inheritdoc/>
    public Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(request.Domain, Domain, StringComparison.Ordinal)) {
            return Task.FromResult(DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.UnsupportedRoute));
        }

        ProjectionResponse response = _handler.Project(request);
        DomainProjectionHandlerResult result = response is not null
            && string.Equals(response.ProjectionType, ProjectionType, StringComparison.Ordinal)
                ? DomainProjectionHandlerResult.Completed(response.State.Clone())
                : DomainProjectionHandlerResult.Failed(ProjectionDispatchReasonCodes.MalformedOutcome);

        return Task.FromResult(result);
    }
}
