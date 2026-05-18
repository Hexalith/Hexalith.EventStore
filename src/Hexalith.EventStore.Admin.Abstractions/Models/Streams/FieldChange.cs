using System.Text.Json.Serialization;

using Hexalith.EventStore.Admin.Abstractions.Models;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// A single field change between two aggregate state versions.
/// </summary>
/// <param name="FieldPath">The JSON path to the changed field.</param>
/// <param name="OldValue">The previous value as opaque JSON scalar.</param>
/// <param name="NewValue">The new value as opaque JSON scalar.</param>
public record FieldChange(string FieldPath, string OldValue, string NewValue) {
    /// <summary>Gets the JSON path to the changed field.</summary>
    public string FieldPath { get; } = !string.IsNullOrWhiteSpace(FieldPath)
        ? FieldPath
        : throw new ArgumentException("FieldPath cannot be null, empty, or whitespace.", nameof(FieldPath));

    /// <summary>Gets the previous value as opaque JSON scalar when the content is safe to expose.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string OldValue { get; init; } = OldValue ?? throw new ArgumentNullException(nameof(OldValue));

    /// <summary>Gets the new value as opaque JSON scalar when the content is safe to expose.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string NewValue { get; init; } = NewValue ?? throw new ArgumentNullException(nameof(NewValue));

    /// <summary>Gets the previous redacted value descriptor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdminRedactedContent? OldContent { get; init; }

    /// <summary>Gets the new redacted value descriptor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdminRedactedContent? NewContent { get; init; }

    /// <summary>Creates a field change whose values are represented by redacted descriptors.</summary>
    public static FieldChange Redacted(string fieldPath, AdminRedactedContent oldContent, AdminRedactedContent newContent)
        => new(fieldPath, "{}", "{}") {
            OldValue = null!,
            NewValue = null!,
            OldContent = oldContent ?? throw new ArgumentNullException(nameof(oldContent)),
            NewContent = newContent ?? throw new ArgumentNullException(nameof(newContent))
        };

    /// <summary>
    /// Returns a string representation with OldValue and NewValue redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"FieldChange {{ FieldPath = {FieldPath}, OldValue = [REDACTED], NewValue = [REDACTED] }}";
}
