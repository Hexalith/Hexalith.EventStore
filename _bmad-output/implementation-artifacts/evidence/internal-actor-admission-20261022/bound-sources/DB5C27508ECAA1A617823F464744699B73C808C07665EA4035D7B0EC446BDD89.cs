using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Metadata stored at the aggregate metadata key tracking the current event sequence.
/// </summary>
/// <param name="CurrentSequence">The last persisted event sequence number.</param>
/// <param name="LastModified">When the aggregate was last modified.</param>
/// <param name="ETag">Optional ETag for optimistic concurrency (Story 3.7+).</param>
/// <param name="RetainedFloor">Inclusive first retained envelope sequence. Legacy untrimmed streams start at one.</param>
public record AggregateMetadata(long CurrentSequence, DateTimeOffset LastModified, string? ETag, long RetainedFloor = 1)
{
    /// <summary>Gets optional actor-owned evidence over the complete stored stream prefix.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SourceProof? SourceProof { get; init; }

    /// <summary>Gets durable actor-owned erasure intent, saved before any event-key deletion.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SourceErasurePending { get; init; }

    /// <summary>
    /// Initializes metadata for a legacy untrimmed stream whose retained floor is sequence one.
    /// This preserves the published three-parameter constructor. JSON binds through it; a
    /// persisted <see cref="RetainedFloor"/> is then applied through its init accessor.
    /// </summary>
    /// <param name="CurrentSequence">The last persisted event sequence number.</param>
    /// <param name="LastModified">When the aggregate was last modified.</param>
    /// <param name="ETag">Optional ETag for optimistic concurrency.</param>
    [JsonConstructor]
    public AggregateMetadata(long CurrentSequence, DateTimeOffset LastModified, string? ETag)
        : this(CurrentSequence, LastModified, ETag, 1)
    {
    }

    /// <summary>Deconstructs the published three-member shape.</summary>
    /// <param name="CurrentSequence">The last persisted event sequence number.</param>
    /// <param name="LastModified">When the aggregate was last modified.</param>
    /// <param name="ETag">Optional ETag for optimistic concurrency.</param>
    public void Deconstruct(out long CurrentSequence, out DateTimeOffset LastModified, out string? ETag)
    {
        CurrentSequence = this.CurrentSequence;
        LastModified = this.LastModified;
        ETag = this.ETag;
    }
}
