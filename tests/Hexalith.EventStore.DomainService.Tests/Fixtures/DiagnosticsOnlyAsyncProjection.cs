using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>Contributes a domain solely through asynchronous projection discovery.</summary>
[EventStoreDomain("async-projection-only")]
public sealed class DiagnosticsOnlyAsyncProjection : IAsyncDomainProjectionHandler
{
    /// <inheritdoc/>
    public string Domain => "async-projection-only";

    /// <inheritdoc/>
    public string ProjectionType => "diagnostics-fixture";

    /// <inheritdoc/>
    public Task<DomainProjectionHandlerResult> ProjectAsync(
        ProjectionRequest request,
        string dispatchId,
        CancellationToken cancellationToken)
        => Task.FromResult(DomainProjectionHandlerResult.Completed());
}
