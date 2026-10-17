using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Declares complete-prefix fallback only, without mapped streaming capability.</summary>
internal sealed class GuardedFullReplayProjectionHandler(GuardedTailProjectionHandler inner) : IAsyncDomainProjectionRebuildHandler
{
    public string Domain => inner.Domain;
    public string ProjectionType => inner.ProjectionType;
    public DomainProjectionRebuildSemantics RebuildSemantics => DomainProjectionRebuildSemantics.FullReplay;
    public Task<DomainProjectionRebuildPlan> PrepareRebuildAsync(ProjectionRequest request, string operationId, CancellationToken cancellationToken)
        => inner.PrepareRebuildAsync(request, operationId, cancellationToken);
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken)
        => inner.ProjectAsync(request, dispatchId, cancellationToken);
}
