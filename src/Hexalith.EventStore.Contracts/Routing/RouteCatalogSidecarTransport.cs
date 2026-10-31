namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Actual SDK subscription delegate and configured topic for an existing channel-protected callback.</summary>
public sealed record RouteCatalogSidecarTransport(string Owner, string CodeDigest, string PubSubName, string TopicName);
