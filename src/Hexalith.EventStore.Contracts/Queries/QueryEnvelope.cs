using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Immutable envelope carrying a query request to a projection actor.
/// </summary>
/// <remarks>
/// DAPR actor proxy uses <see cref="DataContractSerializer"/> for method
/// parameters. The data contract attributes are part of the public projection
/// adapter wire contract and must stay additive/backward-compatible.
/// </remarks>
// Namespace pinned to the original Server.Actors CLR namespace so DataContractSerializer
// wire documents remain compatible when callers and callees redeploy independently.
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/Hexalith.EventStore.Server.Actors")]
public record QueryEnvelope {
    /// <summary>
    /// Initializes a new instance of the <see cref="QueryEnvelope"/> record.
    /// </summary>
    /// <param name="tenantId">The tenant identifier used for isolation and routing.</param>
    /// <param name="domain">The domain targeted by the query.</param>
    /// <param name="aggregateId">The aggregate or projection aggregate identifier.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <param name="payload">UTF-8 JSON query payload bytes; use an empty array for tenant-wide list queries.</param>
    /// <param name="correlationId">The correlation identifier propagated from the gateway request.</param>
    /// <param name="userId">The authenticated user identifier, normally the <c>sub</c> claim.</param>
    /// <param name="entityId">Optional entity identifier for entity-scoped routing.</param>
    public QueryEnvelope(
        string tenantId,
        string domain,
        string aggregateId,
        string queryType,
        byte[] payload,
        string correlationId,
        string userId,
        string? entityId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
        QueryType = queryType;
        Payload = payload;
        CorrelationId = correlationId;
        UserId = userId;
        EntityId = entityId;
    }

    /// <summary>
    /// Gets the tenant identifier used for isolation and routing.
    /// </summary>
    [DataMember]
    public string TenantId { get; init; }

    /// <summary>
    /// Gets the domain targeted by the query.
    /// </summary>
    [DataMember]
    public string Domain { get; init; }

    /// <summary>
    /// Gets the aggregate or projection aggregate identifier.
    /// </summary>
    [DataMember]
    public string AggregateId { get; init; }

    /// <summary>
    /// Gets the query type discriminator.
    /// </summary>
    [DataMember]
    public string QueryType { get; init; }

    /// <summary>
    /// Gets the UTF-8 JSON query payload bytes.
    /// </summary>
    [DataMember]
    public byte[] Payload { get; init; }

    /// <summary>
    /// Gets the correlation identifier propagated from the gateway request.
    /// </summary>
    [DataMember]
    public string CorrelationId { get; init; }

    /// <summary>
    /// Gets the authenticated user identifier, normally the <c>sub</c> claim.
    /// </summary>
    [DataMember]
    public string UserId { get; init; }

    /// <summary>
    /// Gets the optional entity identifier for entity-scoped routing.
    /// </summary>
    [DataMember]
    public string? EntityId { get; init; }

    /// <summary>
    /// Gets the aggregate identity represented by the tenant, domain, and aggregate ID.
    /// </summary>
    public AggregateIdentity AggregateIdentity
        => new(TenantId, Domain, AggregateId);

    /// <summary>
    /// Returns a diagnostic string with payload bytes redacted.
    /// </summary>
    /// <returns>A redacted diagnostic representation.</returns>
    public override string ToString()
        => $"QueryEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, QueryType = {QueryType}, CorrelationId = {CorrelationId}, UserId = {UserId}{(EntityId is not null ? $", EntityId = {EntityId}" : "")}, Payload = [REDACTED {Payload.Length} bytes] }}";
}
