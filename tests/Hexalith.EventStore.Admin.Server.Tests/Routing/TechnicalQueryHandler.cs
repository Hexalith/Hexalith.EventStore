using System.Text.Json;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

/// <summary>Actual installed SDK query target keeping the complete schema3 catalog nonempty in transport tests.</summary>
internal sealed class TechnicalQueryHandler : IDomainQueryHandler
{
    internal static RouteContractDeclaration Declaration { get; } = new("technical-fixture-query", "query", "catalog-test", "technical-query", null,
        "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
        new(typeof(TechnicalQueryHandler).FullName!, "exact", ["technical-query"], "domain-service", "computed", []));
    public string Domain => "catalog-test";
    public string QueryType => "technical-query";
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { declared = true })));
    }
}
