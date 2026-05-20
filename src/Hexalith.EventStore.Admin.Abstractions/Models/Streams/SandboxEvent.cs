using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// Captures one event that would be produced by a sandbox command execution.
/// No events are actually persisted — this represents a hypothetical output.
/// </summary>
/// <param name="Index">The 0-based position in the produced events list.</param>
/// <param name="EventTypeName">The event type name.</param>
/// <param name="PayloadJson">The event payload as a JSON string.</param>
/// <param name="IsRejection">True if this event is a domain rejection, false for state-change events.</param>
public record SandboxEvent(
    int Index,
    string EventTypeName,
    string PayloadJson,
    bool IsRejection) {
    /// <summary>Gets the event type name.</summary>
    public string EventTypeName { get; } = EventTypeName ?? string.Empty;

    /// <summary>Gets the event payload JSON when the content is safe to expose.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string PayloadJson { get; init; } = PayloadJson ?? string.Empty;

    /// <summary>Gets the redacted event payload descriptor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdminRedactedContent? Payload { get; init; }

    /// <summary>Creates a sandbox event whose payload is represented by a redacted descriptor.</summary>
    public static SandboxEvent Redacted(int index, string eventTypeName, bool isRejection, AdminRedactedContent payload)
        => new(index, eventTypeName, "{}", isRejection) {
            PayloadJson = null!,
            Payload = payload ?? throw new ArgumentNullException(nameof(payload))
        };

    /// <summary>
    /// Returns a string representation with PayloadJson redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"SandboxEvent {{ Index = {Index}, EventTypeName = {EventTypeName}, PayloadJson = [REDACTED], IsRejection = {IsRejection} }}";
}
