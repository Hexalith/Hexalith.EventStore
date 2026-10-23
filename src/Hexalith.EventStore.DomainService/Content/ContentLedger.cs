namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Single CAS visibility authority for one bounded scope; never contains body bytes.</summary>
internal sealed record ContentLedger(
    int Schema,
    Dictionary<string, ContentStoredUpload> Uploads,
    Dictionary<string, ContentStoredPublication> Publications,
    Dictionary<string, ContentChange> Bindings,
    HashSet<string> Tombstones)
{
    /// <summary>Creates empty metadata.</summary>
    public static ContentLedger Empty() => new(1, new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));
}
