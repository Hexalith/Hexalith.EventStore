namespace Hexalith.EventStore.Client.Projections;

/// <summary>One unchanged selected source key, including certified absence.</summary>
/// <param name="LogicalKey">The complete source inventory key.</param>
/// <param name="ExpectedETag">The original source ETag, or empty for absence.</param>
/// <param name="ContentDigest">The canonical JSON SHA256 digest, or null for absence.</param>
public sealed record SharedProjectionAbortRepairGuard(string LogicalKey, string ExpectedETag, string? ContentDigest)
{
    /// <summary>Gets the original effective physical source generation.</summary>
    public long? SourceGeneration { get; init; }

    /// <summary>Gets the original copy-on-write pointer ETag.</summary>
    public string? PointerETag { get; init; }
}
