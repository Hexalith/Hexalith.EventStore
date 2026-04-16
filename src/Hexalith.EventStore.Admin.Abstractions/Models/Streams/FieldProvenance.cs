namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Per-field provenance tracking which event last changed each field in aggregate state.
/// </summary>
/// <param name="FieldPath">JSON path to the field (e.g., "Count", "Status.IsActive").</param>
/// <param name="CurrentValue">The current value as opaque JSON string.</param>
/// <param name="PreviousValue">The value before the last change; empty if the field was introduced by the last change event.</param>
/// <param name="LastChangedAtSequence">Sequence number of the event that last set this field; -1 if changed before the blame analysis window.</param>
/// <param name="LastChangedAtTimestamp">Timestamp of the event that last changed this field.</param>
/// <param name="LastChangedByEventType">Event type name that last changed this field.</param>
/// <param name="LastChangedByCorrelationId">Correlation ID tracing the originating request.</param>
/// <param name="LastChangedByUserId">User who initiated the command that changed this field.</param>
public record FieldProvenance(
    string FieldPath,
    string CurrentValue,
    string PreviousValue,
    long LastChangedAtSequence,
    DateTimeOffset LastChangedAtTimestamp,
    string LastChangedByEventType,
    string LastChangedByCorrelationId,
    string LastChangedByUserId) {
    /// <summary>Gets the JSON path to the field.</summary>
    public string FieldPath { get; } = FieldPath ?? string.Empty;

    /// <summary>Gets the current value as opaque JSON string.</summary>
    public string CurrentValue { get; } = CurrentValue ?? string.Empty;

    /// <summary>Gets the previous value; empty if the field was introduced by the last change event.</summary>
    public string PreviousValue { get; } = PreviousValue ?? string.Empty;

    /// <summary>Gets the event type name that last changed this field.</summary>
    public string LastChangedByEventType { get; } = LastChangedByEventType ?? string.Empty;

    /// <summary>Gets the correlation ID tracing the originating request.</summary>
    public string LastChangedByCorrelationId { get; } = LastChangedByCorrelationId ?? string.Empty;

    /// <summary>Gets the user who initiated the command that changed this field.</summary>
    public string LastChangedByUserId { get; } = LastChangedByUserId ?? string.Empty;

    /// <summary>
    /// Returns a string representation with CurrentValue and PreviousValue redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"FieldProvenance {{ FieldPath = {FieldPath}, CurrentValue = [REDACTED], PreviousValue = [REDACTED], LastChangedAtSequence = {LastChangedAtSequence}, LastChangedByEventType = {LastChangedByEventType} }}";
}
