using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Durable dispatch fence held through downstream work; disposal releases only its own sequence.</summary>
public sealed class RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release) : IAsyncDisposable
{
    private static readonly object _stateGate = new();
    private bool _disposed;
    private bool _closedForExecution;
    private bool _retained;
    private int _remoteCalls;
    private TaskCompletionSource? _remoteDrain;

    private readonly object? _metadataIssuer;
    private readonly byte[]? _metadataActivation;

    /// <summary>Coordinates original private scope closure with actual remote enrollment.</summary>
    internal static object MetadataLifetimeGate => _stateGate;

    /// <summary>Marks only a real, durably acquired private metadata scope with its actual issuing authority.</summary>
    internal RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release, object metadataIssuer, byte[] activation)
        : this(snapshot, release)
    {
        ArgumentNullException.ThrowIfNull(metadataIssuer);
        _metadataIssuer = metadataIssuer;
        _metadataActivation = activation.ToArray();
    }

    /// <summary>Gets whether this lease carries a platform-issued metadata scope marker.</summary>
    internal bool HasMetadataIssuer => _metadataIssuer is not null;

    /// <summary>Requires this exact issuer and an open original execution lifetime before nested metadata work.</summary>
    internal void RequireMetadataIssuer(object issuer)
    {
        lock (_stateGate)
            if (!ReferenceEquals(_metadataIssuer, issuer) || _closedForExecution)
                throw new RouteCatalogUnavailableException("Nested metadata requires the actual issuing authority's open receiver lease.");
    }

    /// <summary>Gets this dispatch's immutable authority.</summary>
    public CommittedRouteCatalogSnapshot Snapshot { get; } = snapshot;

    /// <summary>The immutable activation that this authority validated when acquiring the lease.</summary>
    internal RouteCatalogActivation? Activation { get; init; }

    /// <summary>Copies the issuer's privately retained frozen activation for fresh nested validation.</summary>
    internal byte[] GetMetadataActivation(object issuer)
    {
        lock (_stateGate)
        {
            RequireMetadataIssuer(issuer);
            return _metadataActivation!.ToArray();
        }
    }

    /// <summary>Refuses creating new work from a receiver lifetime that has already closed.</summary>
    internal void RequireOpenForExecution()
    {
        lock (_stateGate)
            if (_closedForExecution) throw new RouteCatalogUnavailableException("A disposed catalog dispatch scope cannot admit detached work.");
    }

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
            foreach (var lease in leases)
            {
                if (--lease._remoteCalls != 0) continue;
                lease._remoteDrain?.TrySetResult();
                lease._remoteDrain = null;
            }
        }
    }

    /// <summary>Drains actual enrolled calls for an independent hosted owner without clearing ambiguous fences.</summary>
    internal Task DrainRemoteCallsAsync()
    {
        lock (_stateGate) return _remoteCalls == 0 ? Task.CompletedTask
            : (_remoteDrain ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
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
