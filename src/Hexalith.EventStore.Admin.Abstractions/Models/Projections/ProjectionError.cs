namespace Hexalith.EventStore.Admin.Abstractions.Models.Projections;

/// <summary>
/// An error encountered during projection processing.
/// </summary>
/// <param name="Position">The event position where the error occurred.</param>
/// <param name="Timestamp">When the error occurred.</param>
/// <param name="Message">The error message.</param>
/// <param name="EventTypeName">The type name of the event that caused the error, if known.</param>
public record ProjectionError(long Position, DateTimeOffset Timestamp, string Message, string? EventTypeName)
{
    /// <summary>Gets the error message.</summary>
    public string Message { get; } = !string.IsNullOrWhiteSpace(Message)
        ? Message
        : throw new ArgumentException("Message cannot be null, empty, or whitespace.", nameof(Message));
}
