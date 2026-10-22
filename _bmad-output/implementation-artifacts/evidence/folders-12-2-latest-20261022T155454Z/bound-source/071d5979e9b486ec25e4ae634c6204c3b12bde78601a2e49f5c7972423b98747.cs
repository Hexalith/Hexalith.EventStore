namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures one event in a correlation trace map, representing an event produced by a command.
/// </summary>
/// <param name="SequenceNumber">The 1-based sequence number of the event in the aggregate stream.</param>
/// <param name="EventTypeName">The fully qualified event type name.</param>
/// <param name="Timestamp">When the event was recorded.</param>
/// <param name="CausationId">The causation identifier linking this event to its cause.</param>
/// <param name="IsRejection">True if this is a rejection event.</param>
public record TraceMapEvent(
    long SequenceNumber,
    string EventTypeName,
    DateTimeOffset Timestamp,
    string? CausationId,
    bool IsRejection) {
    /// <summary>Gets the fully qualified event type name.</summary>
    public string EventTypeName { get; } = EventTypeName ?? string.Empty;

    /// <summary>Gets the causation identifier linking this event to its cause.</summary>
    public string? CausationId { get; } = CausationId;

    /// <summary>
    /// Returns a string representation (SEC-5 safe — trace map events carry no payloads).
    /// </summary>
    public override string ToString()
        => $"TraceMapEvent {{ SequenceNumber = {SequenceNumber}, EventTypeName = {EventTypeName}, Timestamp = {Timestamp:O}, CausationId = {CausationId ?? "(none)"}, IsRejection = {IsRejection} }}";
}
