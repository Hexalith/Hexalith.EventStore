
using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Immutable envelope carrying a query request to a projection actor.
/// Mirrors CommandEnvelope structure but without Extensions or CausationId
/// (queries are stateless reads with no side effects).
/// </summary>
/// <remarks>
/// DAPR actor proxy uses DataContractSerializer for method parameters.
/// [DataContract]/[DataMember] attributes are MANDATORY — without them,
/// byte[] and JsonElement fields may not serialize correctly across the
/// actor proxy boundary, causing silent data corruption or runtime failures.
/// </remarks>
[DataContract]
public record QueryEnvelope {
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

    [DataMember]
    public string TenantId { get; init; }

    [DataMember]
    public string Domain { get; init; }

    [DataMember]
    public string AggregateId { get; init; }

    [DataMember]
    public string QueryType { get; init; }

    [DataMember]
    public byte[] Payload { get; init; }

    [DataMember]
    public string CorrelationId { get; init; }

    [DataMember]
    public string UserId { get; init; }

    [DataMember]
    public string? EntityId { get; init; }

    public AggregateIdentity AggregateIdentity
        => new(TenantId, Domain, AggregateId);

    public override string ToString()
        => $"QueryEnvelope {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, QueryType = {QueryType}, CorrelationId = {CorrelationId}, UserId = {UserId}{(EntityId is not null ? $", EntityId = {EntityId}" : "")}, Payload = [REDACTED {Payload.Length} bytes] }}";
}
