using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.DomainService.Tests;

public abstract class ThrowingCatalogQueryMetadataProbe : IDomainQueryHandler
{
    public static RouteContractDeclaration CatalogDeclaration => throw new InvalidOperationException("never-print-owner-secret");
    public string Domain => "other";
    public string QueryType => "get-other";
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken) => throw new NotSupportedException();
}
