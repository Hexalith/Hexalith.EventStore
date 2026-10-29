using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Adapts shared SDK RPC tracking to atomic enrollment of the captured hosting leases.</summary>
internal sealed class RouteCatalogRemoteBoundary(IReadOnlyList<RouteCatalogDispatchLease> leases) : IRouteCatalogRemoteBoundary
{
    /// <inheritdoc/>
    public void Enroll() => RouteCatalogDispatchLease.EnrollRemote(leases);

    /// <inheritdoc/>
    public void Retain()
    {
        foreach (RouteCatalogDispatchLease lease in leases) lease.Retain();
    }

    /// <inheritdoc/>
    public void Complete() => RouteCatalogDispatchLease.CompleteRemote(leases);
}
