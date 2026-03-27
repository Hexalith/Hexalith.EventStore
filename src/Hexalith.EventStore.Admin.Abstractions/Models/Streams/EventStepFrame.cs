namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Combines event metadata, aggregate state, and field changes in a single response
/// for one step-through debugging position in an aggregate's event history.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="SequenceNumber">The current position (1-based sequence number).</param>
/// <param name="EventTypeName">The type name of the event at this position.</param>
/// <param name="Timestamp">When this event was recorded.</param>
/// <param name="CorrelationId">Correlation ID tracing the originating request.</param>
/// <param name="CausationId">Causation ID linking to the triggering command or event.</param>
/// <param name="UserId">User who initiated the command that produced this event.</param>
/// <param name="EventPayloadJson">Raw event payload JSON for inspection.</param>
/// <param name="StateJson">Aggregate state JSON after applying this event.</param>
/// <param name="FieldChanges">Fields that changed from the previous state to the state after this event.</param>
/// <param name="TotalEvents">Total event count in the stream for position display.</param>
public record EventStepFrame(
    string TenantId,
    string Domain,
    string AggregateId,
    long SequenceNumber,
    string EventTypeName,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string CausationId,
    string UserId,
    string EventPayloadJson,
    string StateJson,
    IReadOnlyList<FieldChange> FieldChanges,
    long TotalEvents)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = TenantId ?? string.Empty;

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = Domain ?? string.Empty;

    /// <summary>Gets the aggregate identifier.</summary>
    public string AggregateId { get; } = AggregateId ?? string.Empty;

    /// <summary>Gets the type name of the event at this position.</summary>
    public string EventTypeName { get; } = EventTypeName ?? string.Empty;

    /// <summary>Gets the correlation ID tracing the originating request.</summary>
    public string CorrelationId { get; } = CorrelationId ?? string.Empty;

    /// <summary>Gets the causation ID linking to the triggering command or event.</summary>
    public string CausationId { get; } = CausationId ?? string.Empty;

    /// <summary>Gets the user who initiated the command that produced this event.</summary>
    public string UserId { get; } = UserId ?? string.Empty;

    /// <summary>Gets the raw event payload JSON for inspection.</summary>
    public string EventPayloadJson { get; } = EventPayloadJson ?? string.Empty;

    /// <summary>Gets the aggregate state JSON after applying this event.</summary>
    public string StateJson { get; } = StateJson ?? string.Empty;

    /// <summary>Gets the fields that changed from the previous state to the state after this event.</summary>
    public IReadOnlyList<FieldChange> FieldChanges { get; } = FieldChanges ?? [];

    /// <summary>Gets a value indicating whether there is a previous event (sequence > 1).</summary>
    public bool HasPrevious => SequenceNumber > 1;

    /// <summary>Gets a value indicating whether there is a next event (sequence &lt; total).</summary>
    public bool HasNext => SequenceNumber < TotalEvents;

    /// <summary>
    /// Returns a string representation with EventPayloadJson, StateJson, and field values redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"EventStepFrame {{ TenantId = {TenantId}, Domain = {Domain}, AggregateId = {AggregateId}, SequenceNumber = {SequenceNumber}, EventTypeName = {EventTypeName}, EventPayloadJson = [REDACTED], StateJson = [REDACTED], FieldChanges = [{FieldChanges.Count} changes], TotalEvents = {TotalEvents} }}";
}
