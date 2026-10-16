namespace Hexalith.EventStore.Client.Projections;

/// <summary>A failed source position and its durable consecutive fold-attempt count.</summary>
public sealed record SharedProjectionFailureStatus(string SourceStream, long SourcePosition, int FailureCount)
{
    /// <summary>Distinguishes a retryable capacity boundary from failed source decoding.</summary>
    public string ReasonCode { get; init; } = "fold-failure";
}
