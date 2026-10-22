namespace Hexalith.EventStore.DomainService.Tests.Processes;

/// <summary>Counts creation and asynchronous disposal of scoped process catalogs.</summary>
internal sealed class AsyncCatalogDisposalTracker
{
    private int _created;
    private int _disposed;

    /// <summary>Gets the number of created catalog instances.</summary>
    public int Created => Volatile.Read(ref _created);

    /// <summary>Gets the number of asynchronously disposed catalog instances.</summary>
    public int Disposed => Volatile.Read(ref _disposed);

    /// <summary>Records a new catalog instance.</summary>
    public void RecordCreated() => Interlocked.Increment(ref _created);

    /// <summary>Records an asynchronously disposed catalog instance.</summary>
    public void RecordDisposed() => Interlocked.Increment(ref _disposed);
}
