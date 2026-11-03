using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.QuerySelectorFixture;

/// <summary>Inert installed owner handler used only by model and in-memory transport controls.</summary>
public sealed class SelectorFixtureQueryHandler : IDomainQueryHandler
{
    /// <inheritdoc/>
    public string Domain => "widget";
    /// <inheritdoc/>
    public string QueryType => "get-widget";
    /// <inheritdoc/>
    public Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new SelectorFixtureQuery(query.AggregateId)), "widgets"));
    }
}
