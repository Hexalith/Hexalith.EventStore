namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Identifies SDK-mapped catalog callbacks independently of a caller-supplied policy name.</summary>
internal sealed class RouteCatalogControlEndpointMetadata
{
    /// <summary>The SDK-owned endpoint marker.</summary>
    internal static RouteCatalogControlEndpointMetadata Instance { get; } = new();
}
