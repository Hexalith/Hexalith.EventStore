namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Information about a DAPR pub/sub subscription, including topic routing and dead-letter configuration.
/// </summary>
/// <param name="PubSubName">The pub/sub component name this subscription uses.</param>
/// <param name="Topic">The topic pattern (may contain wildcards like *.*.events).</param>
/// <param name="Route">The HTTP path the subscriber handles (e.g., /events/handle).</param>
/// <param name="SubscriptionType">The subscription type: DECLARATIVE or PROGRAMMATIC.</param>
/// <param name="DeadLetterTopic">The dead-letter topic, or null if not configured.</param>
public record DaprSubscriptionInfo(
    string PubSubName,
    string Topic,
    string Route,
    string SubscriptionType,
    string? DeadLetterTopic) {
    /// <summary>Gets the pub/sub component name.</summary>
    public string PubSubName { get; } = !string.IsNullOrWhiteSpace(PubSubName)
        ? PubSubName
        : throw new ArgumentException("PubSubName cannot be null, empty, or whitespace.", nameof(PubSubName));

    /// <summary>Gets the topic pattern.</summary>
    public string Topic { get; } = !string.IsNullOrWhiteSpace(Topic)
        ? Topic
        : throw new ArgumentException("Topic cannot be null, empty, or whitespace.", nameof(Topic));

    /// <summary>Gets the subscriber route.</summary>
    public string Route { get; } = !string.IsNullOrWhiteSpace(Route)
        ? Route
        : throw new ArgumentException("Route cannot be null, empty, or whitespace.", nameof(Route));

    /// <summary>Gets the subscription type.</summary>
    public string SubscriptionType { get; } = !string.IsNullOrWhiteSpace(SubscriptionType)
        ? SubscriptionType
        : throw new ArgumentException("SubscriptionType cannot be null, empty, or whitespace.", nameof(SubscriptionType));
}
