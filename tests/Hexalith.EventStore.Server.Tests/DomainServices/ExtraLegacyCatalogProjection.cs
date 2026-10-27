using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class ExtraLegacyCatalogProjection : IDomainProjectionHandler
{
    public string Domain => "extra";
    public ProjectionResponse Project(ProjectionRequest request) => throw new NotSupportedException();
}
