using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Restores the enclosing RPC boundary without disposing its receiver lease.</summary>
internal sealed class RouteCatalogRemoteScope(RouteCatalogDispatchLease lease, bool isolated = false) : IDisposable
{
    private readonly IRouteCatalogRemoteBoundary? _priorBoundary = RouteCatalogRemoteExecution.Current;
    private readonly IReadOnlyList<RouteCatalogDispatchLease>? _prior = RouteCatalogRemoteCall.Push(lease, isolated);

    /// <inheritdoc/>
    public void Dispose()
    {
        RouteCatalogRemoteCall.Restore(_prior);
        RouteCatalogRemoteExecution.Current = _priorBoundary;
    }
}
