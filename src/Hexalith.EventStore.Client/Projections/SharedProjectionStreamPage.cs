namespace Hexalith.EventStore.Client.Projections;

/// <summary>One immutable page of logical keys reserved by streamed staging.</summary>
/// <param name="Ordinal">The zero-based page position.</param>
/// <param name="Digest">The canonical digest of the page.</param>
/// <param name="Keys">The logical keys in page order.</param>
internal sealed record SharedProjectionStreamPage(int Ordinal, string Digest, string[] Keys)
{
    /// <summary>Retirements captured with the page before staging, so cleanup survives partial deletion.</summary>
    public SharedProjectionRetiredValue[] RetiredValues { get; init; } = [];
}
