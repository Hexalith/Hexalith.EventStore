using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Locates the latest and preceding immutable value of one sparse shared-projection key.</summary>
internal sealed record SharedProjectionCopyOnWritePointer(
    long LatestGeneration,
    bool LatestDeleted,
    long PreviousGeneration,
    bool PreviousDeleted)
{
    /// <summary>The inclusive deadline of the latest value; retained after provider expiry.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? LatestExpiresAtUtc { get; init; }

    /// <summary>The inclusive deadline of the preceding inherited value.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? PreviousExpiresAtUtc { get; init; }

    /// <summary>The obsolete physical generation deleted with the latest pointer update.</summary>
    public long? RetiredGeneration { get; init; }
}
