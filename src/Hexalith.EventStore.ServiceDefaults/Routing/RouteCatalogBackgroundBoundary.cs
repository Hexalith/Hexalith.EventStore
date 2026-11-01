using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Preserves actual RPC enrollment and admits detached work under a separately owned lease of this exact run.</summary>
internal sealed class RouteCatalogBackgroundBoundary(IRouteCatalogRemoteBoundary inner, IRouteCatalogAuthority authority,
    RouteCatalogDispatchLease parentLease, bool allowLegacyFinite = false) : IRouteCatalogRemoteBoundary, IRouteCatalogBackgroundBoundary
{
    public void Enroll() => inner.Enroll();
    public void Retain() => inner.Retain();
    public void Complete() => inner.Complete();

    public async Task<IRouteCatalogBackgroundWork> AcquireAsync(CancellationToken cancellationToken)
    {
        parentLease.RequireOpenForExecution();
        var parent = parentLease.Snapshot;
        var lease = await authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        try { parentLease.RequireOpenForExecution(); }
        catch { await lease.DisposeAsync().ConfigureAwait(false); throw; }
        var child = lease.Snapshot;
        if (parent.ActivationBindingDigest is null && !allowLegacyFinite
            || string.IsNullOrWhiteSpace(parent.LocalAppId) || parent.AuthorityCoordinates is null
            || child.RootDigest != parent.RootDigest || child.Epoch != parent.Epoch
            || child.ActivationBindingDigest != parent.ActivationBindingDigest || child.LocalAppId != parent.LocalAppId
            || child.AuthorityCoordinates != parent.AuthorityCoordinates)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw new RouteCatalogUnavailableException("Detached technical work cannot cross its admitted run or receiver identity.");
        }
        return new RouteCatalogBackgroundWork(lease, authority, allowLegacyFinite);
    }
}
