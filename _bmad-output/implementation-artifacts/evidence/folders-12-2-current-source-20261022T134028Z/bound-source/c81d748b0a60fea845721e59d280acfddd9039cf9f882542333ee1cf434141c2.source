namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Context for domain-event consumption, providing envelope metadata to handlers.
/// </summary>
/// <remarks>
/// Generalizes the per-domain consumer contexts domain modules previously hand-wrote (e.g.
/// <c>TenantEventContext</c>). Identifier fields are compared case-sensitively (Ordinal) by convention;
/// canonical casing is the publisher's responsibility.
/// </remarks>
/// <param name="TenantId">The tenant scope the event was published under.</param>
/// <param name="AggregateId">The aggregate identifier the event belongs to.</param>
/// <param name="MessageId">The unique event message ID (ULID) used for idempotency.</param>
/// <param name="SequenceNumber">The event sequence number within the aggregate.</param>
/// <param name="Timestamp">When the event was persisted.</param>
/// <param name="CorrelationId">The request correlation ID for tracing.</param>
public record EventStoreDomainEventContext(
    string TenantId,
    string AggregateId,
    string MessageId,
    long SequenceNumber,
    DateTimeOffset Timestamp,
    string CorrelationId) {
    /// <summary>Gets the EventStore domain that published the event, when present.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets the global stream position, when present.</summary>
    public long? GlobalPosition { get; init; }

    /// <summary>Gets the causation identifier, when present.</summary>
    public string? CausationId { get; init; }

    /// <summary>Gets the user identifier that produced the event, when present.</summary>
    public string? UserId { get; init; }
}
