namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// An entry in a stream timeline showing commands, events, or queries.
/// </summary>
/// <param name="SequenceNumber">The sequence number within the stream.</param>
/// <param name="Timestamp">When the entry occurred.</param>
/// <param name="EntryType">The type of timeline entry.</param>
/// <param name="TypeName">The fully qualified type name of the command, event, or query.</param>
/// <param name="CorrelationId">The correlation identifier for request tracing.</param>
/// <param name="UserId">The user who initiated the action.</param>
public record TimelineEntry(
    long SequenceNumber,
    DateTimeOffset Timestamp,
    TimelineEntryType EntryType,
    string TypeName,
    string CorrelationId,
    string? UserId) {
    /// <summary>Gets the fully qualified type name.</summary>
    public string TypeName { get; } = !string.IsNullOrWhiteSpace(TypeName)
        ? TypeName
        : throw new ArgumentException("TypeName cannot be null, empty, or whitespace.", nameof(TypeName));

    /// <summary>Gets the correlation identifier.</summary>
    public string CorrelationId { get; } = !string.IsNullOrWhiteSpace(CorrelationId)
        ? CorrelationId
        : throw new ArgumentException("CorrelationId cannot be null, empty, or whitespace.", nameof(CorrelationId));

    /// <summary>
    /// Returns a string representation with no payload data exposed (SEC-5).
    /// </summary>
    public override string ToString()
        => $"TimelineEntry {{ SequenceNumber = {SequenceNumber}, Timestamp = {Timestamp}, EntryType = {EntryType}, TypeName = {TypeName}, CorrelationId = {CorrelationId}, UserId = {UserId} }}";
}
