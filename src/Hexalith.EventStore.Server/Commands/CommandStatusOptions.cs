namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Configuration options for command status tracking.
/// </summary>
public record CommandStatusOptions
{
    /// <summary>Gets the TTL in seconds for status entries. Default: 86400 (24 hours).</summary>
    public int TtlSeconds { get; init; } = CommandStatusConstants.DefaultTtlSeconds;

    /// <summary>Gets the DAPR state store component name.</summary>
    public string StateStoreName { get; init; } = CommandStatusConstants.DefaultStateStoreName;
}
