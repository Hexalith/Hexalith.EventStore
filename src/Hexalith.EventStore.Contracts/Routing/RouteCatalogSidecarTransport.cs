using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Actual SDK subscription delegate and configured topic for an existing channel-protected callback.</summary>
public sealed record RouteCatalogSidecarTransport(string Owner, string CodeDigest, string PubSubName, string TopicName)
{
    /// <summary>Gets the explicit subscriber skip policy declared by both the receiver and committed catalog.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EventStoreSubscriptionSkipPolicy AcknowledgedSkips { get; init; }
}
