namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Configures bounded correlation compatibility index behavior.
/// </summary>
public sealed record CommandCorrelationIndexOptions
{
    /// <summary>Gets the maximum number of live message mappings per tenant and correlation identifier.</summary>
    public int Capacity { get; init; } = 128;

    /// <summary>Gets the maximum ETag conflict retries after the initial write attempt.</summary>
    public int MaxConcurrencyRetries { get; init; } = 3;
}
