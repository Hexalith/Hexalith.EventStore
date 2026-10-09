using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Reads the ordinary legacy host store alongside the isolated logical provider.</summary>
/// <param name = "store">The ordinary legacy read-store registration.</param>
public sealed class DaprLogicalQueryLegacyHandler(IReadModelStore store) : IDomainQueryHandler
{
    public string Domain => "d";
    public string QueryType => "legacy";

    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ReadModelEntry<DaprLogicalQueryValue> row = await store.GetAsync<DaprLogicalQueryValue>("legacy", "physical:legacy", cancellationToken);
        return new QueryResult(true, JsonSerializer.SerializeToUtf8Bytes(row.Value));
    }
}
