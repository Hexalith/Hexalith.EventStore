using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Actual compiled query handler of the explicitly separate Development fixture.</summary>
internal sealed class CatalogLiveQueryHandler : IDomainQueryHandler
{
    public static RouteContractDeclaration CatalogDeclaration { get; } = new("fixture-computed-query", "query", "catalog-test", "get-catalog-value",
        null, "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(CatalogLiveQueryHandler).FullName!, "exact", ["get-catalog-value"], "domain-service", "computed", []));
    public string Domain => "catalog-test";
    public string QueryType => "get-catalog-value";
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { source = "declared-computed-handler" })));
    }
}
