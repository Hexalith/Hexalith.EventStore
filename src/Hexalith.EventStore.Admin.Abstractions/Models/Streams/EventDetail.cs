namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Detailed information about a single event for inspection and diagnosis.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="SequenceNumber">The sequence number within the stream.</param>
/// <param name="EventTypeName">The fully qualified event type name.</param>
/// <param name="Timestamp">When the event was produced.</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="CausationId">The optional causation identifier.</param>
/// <param name="UserId">The user who initiated the action, if known.</param>
/// <param name="PayloadJson">The serialized event payload as opaque JSON.</param>
public record EventDetail(
    string TenantId,
    string Domain,
    string AggregateId,
    long SequenceNumber,
    string EventTypeName,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string? CausationId,
    string? UserId,
    string PayloadJson)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = !string.IsNullOrWhiteSpace(AggregateId)
        ? AggregateId
        : throw new ArgumentException("AggregateId cannot be null, empty, or whitespace.", nameof(AggregateId));

    /// <summary>Gets the fully qualified event type name.</summary>
    public string EventTypeName { get; } = !string.IsNullOrWhiteSpace(EventTypeName)
        ? EventTypeName
        : throw new ArgumentException("EventTypeName cannot be null, empty, or whitespace.", nameof(EventTypeName));

    /// <summary>Gets the correlation identifier.</summary>
    public string CorrelationId { get; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>Gets the serialized event payload as opaque JSON.</summary>
    public string PayloadJson { get; } = PayloadJson ?? throw new ArgumentNullException(nameof(PayloadJson));

    /// <summary>
    /// Returns a string representation with PayloadJson redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"EventDetail {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, SequenceNumber = {SequenceNumber}, EventTypeName = {EventTypeName}, Timestamp = {Timestamp}, CorrelationId = {CorrelationId}, CausationId = {CausationId}, UserId = {UserId}, PayloadJson = [REDACTED] }}";
}
