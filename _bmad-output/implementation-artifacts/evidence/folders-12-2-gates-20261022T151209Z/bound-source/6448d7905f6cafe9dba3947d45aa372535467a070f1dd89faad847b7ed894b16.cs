namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>A round-trip-safe state whose final event replaces the earlier values.</summary>
public sealed class ConvergentSnapshotState
{
    /// <summary>Gets or sets the folded values.</summary>
    public int[] Values { get; set; } = [];

    /// <summary>Applies the canonical event payload.</summary>
    /// <param name="payload">The admitted event.</param>
    public void Apply(ConvergentSnapshotEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Values = payload.Value == 42 ? Enumerable.Range(0, 5000).ToArray() : [payload.Value];
    }
}
