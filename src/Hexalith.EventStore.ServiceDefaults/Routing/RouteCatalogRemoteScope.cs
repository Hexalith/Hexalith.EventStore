namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Restores the enclosing RPC boundary without disposing its receiver lease.</summary>
internal sealed class RouteCatalogRemoteScope(RouteCatalogDispatchLease lease) : IDisposable
{
    private readonly IReadOnlyList<RouteCatalogDispatchLease>? _prior = RouteCatalogRemoteCall.Push(lease);

    /// <inheritdoc/>
    public void Dispose() => RouteCatalogRemoteCall.Restore(_prior);
}
