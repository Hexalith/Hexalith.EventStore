namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// One ordered operation within a raw state transaction (transaction-qualified profile).
/// </summary>
/// <param name="Key">The state key.</param>
/// <param name="Value">The raw value bytes (empty for a delete).</param>
/// <param name="IsDelete">Whether the operation removes the key.</param>
/// <param name="ETag">The expected ETag when <paramref name="FirstWrite"/> is set; otherwise empty.</param>
/// <param name="FirstWrite">Whether the operation uses first-write optimistic concurrency.</param>
internal readonly record struct RawTransactionOperation(
    string Key,
    ReadOnlyMemory<byte> Value,
    bool IsDelete,
    string ETag,
    bool FirstWrite);
