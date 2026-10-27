using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.DomainService.Tests;

public abstract class CatalogQueryMetadataProbe : IDomainQueryHandler
{
    public static RouteContractDeclaration CatalogDeclaration { get; } = new("counter-query", "query", "counter", "get-counter", "counter",
        "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(CatalogQueryMetadataProbe).FullName!, "exact", ["get-counter"], "domain-service", "projection", []));
    public string Domain => "counter";
    public string QueryType => "get-counter";
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken) => throw new NotSupportedException();
}
