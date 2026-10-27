using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class ExtraAsyncCatalogProjection : IAsyncDomainProjectionHandler
{
    public string Domain => "extra";
    public string ProjectionType => "extra";
    public Task<DomainProjectionHandlerResult> ProjectAsync(ProjectionRequest request, string dispatchId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
