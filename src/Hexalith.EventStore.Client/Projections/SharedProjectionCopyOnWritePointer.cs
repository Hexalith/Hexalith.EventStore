namespace Hexalith.EventStore.Client.Projections;

/// <summary>Locates the latest and preceding immutable value of one sparse shared-projection key.</summary>
internal sealed record SharedProjectionCopyOnWritePointer(
    long LatestGeneration,
    bool LatestDeleted,
    long PreviousGeneration,
    bool PreviousDeleted)
{
    /// <summary>The obsolete physical generation deleted with the latest pointer update.</summary>
    public long? RetiredGeneration { get; init; }
}
