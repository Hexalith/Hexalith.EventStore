using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Describes how trustworthy an actor type's active instance count is.
/// </summary>
/// <remarks>
/// Ordinals are pinned for wire compatibility. New members must be appended.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DaprActorCountStatus {
    /// <summary>The source returned an exact count for this actor type.</summary>
    Exact = 0,

    /// <summary>The count source was reachable but did not provide a count for this actor type.</summary>
    Unavailable = 1,

    /// <summary>The count came from a bounded or fallback source and must not be treated as complete inventory.</summary>
    SourceLimited = 2,
}
