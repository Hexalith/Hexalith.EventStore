namespace Hexalith.EventStore.Admin.Abstractions.Models.Streams;

/// <summary>
/// A single field change between two aggregate state versions.
/// </summary>
/// <param name="FieldPath">The JSON path to the changed field.</param>
/// <param name="OldValue">The previous value as opaque JSON scalar.</param>
/// <param name="NewValue">The new value as opaque JSON scalar.</param>
public record FieldChange(string FieldPath, string OldValue, string NewValue)
{
    /// <summary>Gets the JSON path to the changed field.</summary>
    public string FieldPath { get; } = !string.IsNullOrWhiteSpace(FieldPath)
        ? FieldPath
        : throw new ArgumentException("FieldPath cannot be null, empty, or whitespace.", nameof(FieldPath));

    /// <summary>Gets the previous value as opaque JSON scalar.</summary>
    public string OldValue { get; } = OldValue ?? throw new ArgumentNullException(nameof(OldValue));

    /// <summary>Gets the new value as opaque JSON scalar.</summary>
    public string NewValue { get; } = NewValue ?? throw new ArgumentNullException(nameof(NewValue));

    /// <summary>
    /// Returns a string representation with OldValue and NewValue redacted (SEC-5).
    /// </summary>
    public override string ToString()
        => $"FieldChange {{ FieldPath = {FieldPath}, OldValue = [REDACTED], NewValue = [REDACTED] }}";
}
