namespace Hexalith.EventStore.Client.Projections;

/// <summary>One certified originally absent metadata key and its retained corrupt evidence.</summary>
/// <param name="LogicalKey">The selected generation's logical key.</param>
/// <param name="ExpectedETag">The observed concurrency token, or empty for absence.</param>
/// <param name="OriginalJson">The original corrupt JSON, or null for absence.</param>
public sealed record SharedProjectionAbortRepairValue(string LogicalKey, string ExpectedETag, string? OriginalJson)
{
    /// <summary>Gets the original effective physical source generation.</summary>
    public long? SourceGeneration { get; init; }

    /// <summary>Gets the original copy-on-write pointer ETag.</summary>
    public string? PointerETag { get; init; }
}
