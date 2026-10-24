// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed KekInventoryHeadV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="BackendIdDigest">The canonical backendIdDigest field.</param>
/// <param name="ProviderVersionDigest">The canonical providerVersionDigest field.</param>
/// <param name="Generation">The canonical generation field.</param>
/// <param name="PageCount">The canonical pageCount field.</param>
/// <param name="EntryCount">The canonical entryCount field.</param>
/// <param name="UpdatedAtUtc">The canonical updatedAtUtc field.</param>
internal sealed record KekInventoryHeadV1(
    string BackendIdDigest,
    string ProviderVersionDigest,
    ulong Generation,
    uint PageCount,
    ulong EntryCount,
    string UpdatedAtUtc)
{
    /// <summary>Gets the closed durable schema version.</summary>
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(KekInventoryHeadV1);
}
