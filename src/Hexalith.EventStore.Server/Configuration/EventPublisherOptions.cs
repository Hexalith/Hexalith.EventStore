namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for event publication via DAPR pub/sub.
/// Bound to configuration section "EventStore:Publisher".
/// </summary>
public record EventPublisherOptions
{
    /// <summary>Gets the DAPR pub/sub component name.</summary>
    public string PubSubName { get; init; } = "pubsub";
}
