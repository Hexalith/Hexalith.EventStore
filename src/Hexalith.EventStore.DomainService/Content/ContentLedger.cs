namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Single CAS visibility authority for one bounded scope; never contains body bytes.</summary>
internal sealed record ContentLedger(
    int Schema,
    Dictionary<string, ContentStoredUpload> Uploads,
    Dictionary<string, ContentStoredPublication> Publications,
    Dictionary<string, ContentChange> Bindings,
    HashSet<string> Tombstones)
{
    /// <summary>Gets released task lease fences; a higher owner-issued fence may resume an uncompleted task.</summary>
    public Dictionary<string, long> QuiescedTaskFences { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Gets monotonic task seals; bodies remain retained and equivalent publication recovery remains allowed.</summary>
    public HashSet<string> SealedTasks { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Creates empty metadata.</summary>
    public static ContentLedger Empty() => new(1, new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));
}
