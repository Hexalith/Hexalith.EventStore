namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Thrown when an event type cannot be deserialized during state rehydration.
/// Domain services must maintain backward-compatible deserialization for all event types.
/// </summary>
public class UnknownEventException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownEventException"/> class.
    /// </summary>
    /// <param name="sequenceNumber">The event sequence number.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="eventTypeName">The event type that could not be deserialized.</param>
    public UnknownEventException(long sequenceNumber, string tenantId, string domain, string aggregateId, string eventTypeName)
        : base($"UnknownEvent during state rehydration: sequence {sequenceNumber}, type '{eventTypeName}', aggregate {tenantId}:{domain}:{aggregateId}. Domain service must maintain backward-compatible deserialization for all event types.") {
        SequenceNumber = sequenceNumber;
        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
        EventTypeName = eventTypeName;
    }

    /// <summary>Gets the event sequence number.</summary>
    public long SequenceNumber { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; }

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; }

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; }

    /// <summary>Gets the event type name that could not be deserialized.</summary>
    public string EventTypeName { get; }
}
