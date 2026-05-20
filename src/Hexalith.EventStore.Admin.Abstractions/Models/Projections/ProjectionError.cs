using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Projections;

/// <summary>
/// An error encountered during projection processing.
/// </summary>
/// <param name="Position">The event position where the error occurred.</param>
/// <param name="Timestamp">When the error occurred.</param>
/// <param name="Message">The error message.</param>
/// <param name="EventTypeName">The type name of the event that caused the error, if known.</param>
public record ProjectionError(long Position, DateTimeOffset Timestamp, string Message, string? EventTypeName) {
    /// <summary>Gets the error message when safe to expose.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; init; } = !string.IsNullOrWhiteSpace(Message)
        ? Message
        : throw new ArgumentException("Message cannot be null, empty, or whitespace.", nameof(Message));

    /// <summary>Gets the redacted diagnostic descriptor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdminRedactedContent? Diagnostic { get; init; }

    /// <summary>Creates a projection error whose diagnostic is represented by a redacted descriptor.</summary>
    public static ProjectionError WithRedactedMessage(long position, DateTimeOffset timestamp, AdminRedactedContent diagnostic, string? eventTypeName)
        => new(position, timestamp, AdminRedactedContent.DefaultPlaceholder, eventTypeName) {
            Message = null!,
            Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic))
        };
}
