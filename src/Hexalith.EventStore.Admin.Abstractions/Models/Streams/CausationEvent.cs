namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// An event within a causation chain.
/// </summary>
/// <param name="SequenceNumber">The sequence number of the event.</param>
/// <param name="EventTypeName">The fully qualified event type name.</param>
/// <param name="Timestamp">When the event was produced.</param>
public record CausationEvent(long SequenceNumber, string EventTypeName, DateTimeOffset Timestamp)
{
    /// <summary>Gets the fully qualified event type name.</summary>
    public string EventTypeName { get; } = !string.IsNullOrWhiteSpace(EventTypeName)
        ? EventTypeName
        : throw new ArgumentException("EventTypeName cannot be null, empty, or whitespace.", nameof(EventTypeName));
}
