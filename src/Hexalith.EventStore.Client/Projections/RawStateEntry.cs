namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A raw byte-state read result: existence, value bytes, and ETag. This is the minimal shape both the
/// DAPR adapter and the in-memory fake expose so the batch protocol runs identically over each.
/// </summary>
/// <param name="Exists">Whether the key is present.</param>
/// <param name="Value">The raw value bytes (empty when absent).</param>
/// <param name="ETag">The current ETag (empty when absent).</param>
internal readonly record struct RawStateEntry(bool Exists, ReadOnlyMemory<byte> Value, string ETag);
