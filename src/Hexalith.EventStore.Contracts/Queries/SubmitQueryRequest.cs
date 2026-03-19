
using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Represents a request to submit a query to the event store.
/// </summary>
/// <param name="Tenant">The tenant identifier for multi-tenant isolation.</param>
/// <param name="Domain">The domain name the query targets.</param>
/// <param name="AggregateId">The aggregate identifier to query.</param>
/// <param name="QueryType">The type discriminator of the query.</param>
/// <param name="Payload">Optional JSON payload containing query parameters.</param>
/// <param name="EntityId">Optional entity identifier for sub-aggregate queries.</param>
public record SubmitQueryRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    JsonElement? Payload = null,
    string? EntityId = null);
