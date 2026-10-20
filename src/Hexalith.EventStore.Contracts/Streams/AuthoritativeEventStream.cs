using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>A complete, authorized source prefix at a stable sampled head.</summary>
/// <param name="Identity">The exact authenticated stream scope.</param>
/// <param name="Head">The inclusive source checkpoint.</param>
/// <param name="ObservedAt">The observation time, never an event's effective time.</param>
/// <param name="Events">The contiguous ordered prefix from one through Head.</param>
/// <param name="ObservationId">The opaque observation identity.</param>
public sealed record AuthoritativeEventStream(
    AggregateIdentity Identity,
    long Head,
    DateTimeOffset ObservedAt,
    IReadOnlyList<StreamReadEvent> Events,
    string ObservationId)
{
    /// <summary>Gets the native prefix that was verified from the exact returned ordered event digests.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeSourceProof? NativeProof { get; init; }
}
