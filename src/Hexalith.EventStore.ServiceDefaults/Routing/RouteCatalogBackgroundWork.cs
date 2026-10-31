using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Replaces inherited request scopes with the separately acquired lifetime of one existing detached workflow.</summary>
internal sealed class RouteCatalogBackgroundWork(RouteCatalogDispatchLease lease, IRouteCatalogAuthority authority) : IRouteCatalogBackgroundWork
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _state;

    public async Task RunAsync(Func<Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        lock (_gate)
        {
            if (_state != 0) throw new RouteCatalogUnavailableException("Detached catalog work is disposed or has already executed.");
            _state = 1;
        }
        try
        {
            lease.RequireOpenForExecution();
            using var scope = new RouteCatalogRemoteScope(lease, isolated: true);
            RouteCatalogRemoteExecution.Current = new RouteCatalogBackgroundBoundary(RouteCatalogRemoteExecution.Current!, authority, lease);
            await execute().ConfigureAwait(false);
        }
        finally
        {
            try { await lease.DisposeAsync().ConfigureAwait(false); }
            finally { lock (_gate) _state = 2; _completed.TrySetResult(); }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_state == 1) return new(_completed.Task);
            _state = 2;
        }
        return lease.DisposeAsync();
    }
}
