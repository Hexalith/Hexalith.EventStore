namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Metadata stored at the aggregate metadata key tracking the current event sequence.
/// </summary>
/// <param name="CurrentSequence">The last persisted event sequence number.</param>
/// <param name="LastModified">When the aggregate was last modified.</param>
/// <param name="ETag">Optional ETag for optimistic concurrency (Story 3.7+).</param>
/// <param name="RetainedFloor">Inclusive first retained envelope sequence. Legacy untrimmed streams start at one.</param>
public record AggregateMetadata(long CurrentSequence, DateTimeOffset LastModified, string? ETag, long RetainedFloor = 1);
