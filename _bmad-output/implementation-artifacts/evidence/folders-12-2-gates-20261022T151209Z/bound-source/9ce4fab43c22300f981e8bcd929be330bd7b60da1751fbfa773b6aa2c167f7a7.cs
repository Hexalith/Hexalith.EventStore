
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Represents a request to submit a query to the event store.
/// </summary>
/// <param name="Tenant">The tenant identifier for multi-tenant isolation.</param>
/// <param name="Domain">The domain name the query targets.</param>
/// <param name="AggregateId">The aggregate identifier to query.</param>
/// <param name="QueryType">The type discriminator of the query.</param>
/// <param name="ProjectionType">Optional projection type used to route the query to the correct persisted read model.</param>
/// <param name="Payload">Optional JSON payload containing query parameters.</param>
/// <param name="EntityId">Optional entity identifier for sub-aggregate queries.</param>
/// <param name="ProjectionActorType">Optional DAPR actor type name hosting the projection. Defaults to the generic "ProjectionActor"; domain services with their own projection actor (e.g., tenants) set this to their actor type.</param>
public record SubmitQueryRequest(
    string Tenant,
    string Domain,
    string AggregateId,
    string QueryType,
    string? ProjectionType = null,
    JsonElement? Payload = null,
    string? EntityId = null,
    string? ProjectionActorType = null) {
    /// <summary>
    /// Gets the optional public paging policy. Omission preserves the legacy unpaged query shape.
    /// </summary>
    public QueryPagingOptions? Paging { get; init; }

    /// <summary>
    /// Gets the optional public search text. Blank search text is normalized as omitted.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Gets optional public filter expressions.
    /// </summary>
    public IReadOnlyList<QueryFilter>? Filters { get; init; }

    /// <summary>
    /// Gets optional public ordering expressions.
    /// </summary>
    public IReadOnlyList<QuerySort>? OrderBy { get; init; }

    /// <summary>
    /// Gets the optional public freshness policy.
    /// </summary>
    public QueryFreshnessPolicy? Freshness { get; init; }

    /// <summary>
    /// Gets unknown top-level JSON properties captured during deserialization for deterministic validation.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}
