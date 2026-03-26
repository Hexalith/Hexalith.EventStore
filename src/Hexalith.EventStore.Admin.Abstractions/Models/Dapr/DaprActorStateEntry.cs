namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// A single state entry for a DAPR actor instance.
/// </summary>
/// <param name="Key">The state key name.</param>
/// <param name="JsonValue">The raw JSON string value from the state store, or null if not found.</param>
/// <param name="EstimatedSizeBytes">The estimated size in bytes of the JSON value.</param>
/// <param name="Found">Whether the key existed in the state store.</param>
public record DaprActorStateEntry(
    string Key,
    string? JsonValue,
    long EstimatedSizeBytes,
    bool Found)
{
    /// <summary>Gets the state key name.</summary>
    public string Key { get; } = !string.IsNullOrWhiteSpace(Key)
        ? Key
        : throw new ArgumentException("Key cannot be null, empty, or whitespace.", nameof(Key));
}
