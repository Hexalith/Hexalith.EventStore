namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Strong reads and atomic first-write comparison over one shared, non-app-prefixed Dapr state key.</summary>
public interface IRouteCatalogStateStore
{
    /// <summary>Gets the independently selected effective coordinates. Custom stores must supply explicit observations.</summary>
    RouteCatalogStateCoordinates? Coordinates => null;

    /// <summary>Reads the persisted cell and its exact concurrency token.</summary>
    Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the entire cell only when its concurrency token still matches.</summary>
    Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken);
}
