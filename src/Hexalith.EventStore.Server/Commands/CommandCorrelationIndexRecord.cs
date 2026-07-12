namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Stores the bounded live message mappings for one tenant-scoped correlation identifier.
/// </summary>
/// <param name="Entries">The live or not-yet-pruned message mappings.</param>
/// <param name="Overflowed">Whether a live mapping could not be represented without eviction.</param>
/// <param name="OverflowExpiresAt">When the bounded overflow ambiguity expires; null preserves fail-closed legacy records.</param>
public sealed record CommandCorrelationIndexRecord(
    List<CommandCorrelationIndexEntry> Entries,
    bool Overflowed,
    DateTimeOffset? OverflowExpiresAt = null);
