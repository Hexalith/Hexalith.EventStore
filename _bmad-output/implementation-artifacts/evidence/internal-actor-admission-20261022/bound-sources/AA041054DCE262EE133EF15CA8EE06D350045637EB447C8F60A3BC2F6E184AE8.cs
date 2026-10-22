namespace Hexalith.EventStore.Client.Projections;

/// <summary>Identifies newly prepared coordinated selections that require isolated SDK completion evidence.</summary>
public static class ReadModelRebuildCompletion
{
    /// <summary>
    /// The versioned content-free receipt discriminator. It is deliberately different from the rich
    /// rebuild scope: older readers derive a different marker key and cannot hydrate the rebuild marker.
    /// This discriminator must be present in the original fingerprint-bound model operations.
    /// </summary>
    public const string ReceiptProjectionName = "rebuild-completion-v1";
}
