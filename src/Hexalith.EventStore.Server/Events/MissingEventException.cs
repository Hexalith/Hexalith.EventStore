namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Thrown when an expected event is missing from the event stream, indicating data corruption.
/// </summary>
public class MissingEventException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingEventException"/> class.
    /// </summary>
    /// <param name="sequenceNumber">The missing event sequence number.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    public MissingEventException(long sequenceNumber, string tenantId, string domain, string aggregateId)
        : base($"Missing event at sequence {sequenceNumber} for aggregate {tenantId}:{domain}:{aggregateId}. This indicates state store data corruption.")
    {
        SequenceNumber = sequenceNumber;
        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
    }

    /// <summary>Gets the missing event sequence number.</summary>
    public long SequenceNumber { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; }

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; }

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; }
}
