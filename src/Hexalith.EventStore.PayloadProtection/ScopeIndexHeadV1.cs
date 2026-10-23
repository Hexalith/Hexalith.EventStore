// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed ScopeIndexHeadV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="Generation">The canonical generation field.</param>
/// <param name="PageCount">The canonical pageCount field.</param>
/// <param name="EntryCount">The canonical entryCount field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
internal sealed record ScopeIndexHeadV1(
    string ScopeDigest,
    ulong Generation,
    uint PageCount,
    uint EntryCount,
    string UpdatedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(ScopeIndexHeadV1);
}
