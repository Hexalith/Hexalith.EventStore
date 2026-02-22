
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Configuration;
/// <summary>
/// Configuration options for event publication via DAPR pub/sub.
/// Bound to configuration section "EventStore:Publisher".
/// </summary>
public record EventPublisherOptions {
    /// <summary>Gets the DAPR pub/sub component name.</summary>
    public string PubSubName { get; init; } = "pubsub";

    /// <summary>Gets the prefix for dead-letter topic names. Used by Story 4.5 for per-subscription dead-letter routing.</summary>
    public string DeadLetterTopicPrefix { get; init; } = "deadletter";

    /// <summary>
    /// Gets the dead-letter topic name for a specific aggregate identity.
    /// Format: {DeadLetterTopicPrefix}.{tenantId}.{domain}.events
    /// </summary>
    /// <param name="identity">The aggregate identity to derive the dead-letter topic from.</param>
    /// <returns>The fully-qualified dead-letter topic name.</returns>
    public string GetDeadLetterTopic(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        return $"{DeadLetterTopicPrefix}.{identity.TenantId}.{identity.Domain}.events";
    }
}
