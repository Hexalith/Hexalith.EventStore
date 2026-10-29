namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Null-safe lease scope that tracks remote calls. Cancellation before a remote call starts retains nothing; once a call is in flight, any failure or cancellation retains the fence.</summary>
public readonly struct RouteCatalogLeaseReleaser : IAsyncDisposable
{
    private readonly RouteCatalogDispatchLease? _lease;
    private readonly IReadOnlyList<RouteCatalogDispatchLease>? _prior;

    /// <summary>Enters one captured dispatch scope, including its asynchronous helper calls.</summary>
    public RouteCatalogLeaseReleaser(RouteCatalogDispatchLease? lease, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        _lease = lease;
        _prior = RouteCatalogRemoteCall.Push(lease);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        RouteCatalogRemoteCall.Restore(_prior);
        return _lease?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
