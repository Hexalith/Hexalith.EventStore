namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Durable dispatch fence held through downstream work; disposal releases only its own sequence.</summary>
public sealed class RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release) : IAsyncDisposable
{
    private static readonly object _stateGate = new();
    private bool _disposed;
    private bool _closedForExecution;
    private bool _retained;
    private int _remoteCalls;

    /// <summary>Gets this dispatch's immutable authority.</summary>
    public CommittedRouteCatalogSnapshot Snapshot { get; } = snapshot;

    /// <summary>Enrolls the complete captured scope before any remote factory can run, or refuses it without effects.</summary>
    internal static void EnrollRemote(IReadOnlyList<RouteCatalogDispatchLease> leases)
    {
        lock (_stateGate)
        {
            if (leases.Any(lease => lease._closedForExecution))
                throw new RouteCatalogUnavailableException("A disposed catalog dispatch scope cannot start remote execution.");
            foreach (var lease in leases) lease._remoteCalls++;
        }
    }

    /// <summary>Unwinds one enrollment after actual completion without clearing a retained durable fence.</summary>
    internal static void CompleteRemote(IReadOnlyList<RouteCatalogDispatchLease> leases)
    {
        lock (_stateGate)
        {
            foreach (var lease in leases) lease._remoteCalls--;
        }
    }

    /// <summary>Retains the durable fence when remote completion is ambiguous. It never expires automatically.</summary>
    public void Retain()
    {
        lock (_stateGate) _retained = true;
    }

    /// <summary>Awaits remote execution and retains this fence if transport completion cannot prove quiescence. A token already cancelled on entry throws before enrollment and retains nothing.</summary>
    public async Task<T> InvokeRemoteAsync<T>(Func<Task<T>> invoke, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        cancellationToken.ThrowIfCancellationRequested();
        RouteCatalogDispatchLease[] leases = [this];
        EnrollRemote(leases);
        try { return await invoke().ConfigureAwait(false); }
        catch { Retain(); throw; }
        finally { CompleteRemote(leases); }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        lock (_stateGate)
        {
            if (_disposed) return;
            _closedForExecution = true;
            _disposed = true;
            if (_remoteCalls != 0) _retained = true;
            if (_retained) return;
        }
        try
        {
            await release().ConfigureAwait(false);
        }
        catch
        {
            // A failed durable release leaves the fence live; allow an explicit retry.
            lock (_stateGate) _disposed = false;
            throw;
        }
    }
}
