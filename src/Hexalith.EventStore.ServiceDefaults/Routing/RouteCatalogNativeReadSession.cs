namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Identifies the one active native-read fence and its exact target within the execution context.</summary>
/// <param name="authority">The authority that acquired the durable lease.</param>
/// <param name="lease">The durable lease held for the read sequence.</param>
/// <param name="targetAppId">The exact EventStore gateway target.</param>
internal sealed class RouteCatalogNativeReadSession(
    IRouteCatalogAuthority authority,
    RouteCatalogDispatchLease lease,
    string targetAppId)
{
    private int _closed;

    /// <summary>Gets the authority that acquired the lease.</summary>
    internal IRouteCatalogAuthority Authority { get; } = authority;

    /// <summary>Gets the retained durable lease.</summary>
    internal RouteCatalogDispatchLease Lease { get; } = lease;

    /// <summary>Gets the only gateway target permitted to reuse the lease.</summary>
    internal string TargetAppId { get; } = targetAppId;

    /// <summary>Refuses detached requests after the enclosing read has finished.</summary>
    internal void RequireOpen()
    {
        if (Volatile.Read(ref _closed) != 0) throw new RouteCatalogUnavailableException("A completed native source read scope cannot admit another request.");
    }

    /// <summary>Closes the read scope before releasing its durable lease.</summary>
    internal void Close() => Volatile.Write(ref _closed, 1);
}
