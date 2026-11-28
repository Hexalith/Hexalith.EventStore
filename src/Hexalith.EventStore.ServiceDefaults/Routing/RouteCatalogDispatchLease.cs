using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Durable dispatch fence held through downstream work; disposal releases only its own sequence.</summary>
public sealed class RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release) : IAsyncDisposable
{
    private static readonly object _stateGate = new();
    private bool _disposed;
    private bool _closedForExecution;
    private bool _retained;
    private bool _sharedReferenceCompleted;
    private bool _releaseSharedFence;
    private int _remoteCalls;
    private TaskCompletionSource? _remoteDrain;

    private readonly object? _issuer;
    private readonly RouteCatalogDispatchLease? _borrowedFrom;
    private readonly object? _metadataIssuer;
    private readonly byte[]? _metadataActivation;

    /// <summary>Coordinates original private scope closure with actual remote enrollment.</summary>
    internal static object MetadataLifetimeGate => _stateGate;

    /// <summary>Marks a real, durably acquired dispatch lease with its actual issuing authority.</summary>
    internal RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release, object issuer)
        : this(snapshot, release)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        _issuer = issuer;
    }

    /// <summary>Marks only a real, durably acquired private metadata scope with its actual issuing authority.</summary>
    internal RouteCatalogDispatchLease(CommittedRouteCatalogSnapshot snapshot, Func<Task> release, object metadataIssuer, byte[] activation)
        : this(snapshot, release, metadataIssuer)
    {
        _metadataIssuer = metadataIssuer;
        _metadataActivation = activation.ToArray();
    }

    private RouteCatalogDispatchLease(RouteCatalogDispatchLease parent)
        : this(parent.Snapshot, static () => Task.CompletedTask)
    {
        RouteCatalogDispatchLease root = parent.Root;
        _borrowedFrom = root;
        _issuer = root._issuer;
        Activation = root.Activation;
    }

    /// <summary>Gets the underlying durable lease owned by the enclosing dispatch scope.</summary>
    internal RouteCatalogDispatchLease Root => _borrowedFrom ?? this;

    /// <summary>Gets whether this lease borrows an enclosing durable fence instead of owning its own.</summary>
    internal bool IsBorrowed => _borrowedFrom is not null;

    /// <summary>Checks whether this lease (or its underlying durable parent) was issued by the exact authority instance.</summary>
    internal bool IsIssuedBy(object issuer) => ReferenceEquals(Root._issuer, issuer);

    /// <summary>Creates a non-owning scoped lease handle bound to an open durable parent lease.</summary>
    internal static RouteCatalogDispatchLease CreateBorrowed(RouteCatalogDispatchLease parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        RouteCatalogDispatchLease root = parent.Root;
        lock (_stateGate)
        {
            if (parent._closedForExecution || root._closedForExecution)
                throw new RouteCatalogUnavailableException("A disposed catalog dispatch scope cannot admit nested work.");
            return new RouteCatalogDispatchLease(root);
        }
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

    /// <summary>Gets the shared durable fence backing this independent dispatch, when enrolled.</summary>
    internal RouteCatalogActiveGenerationLease? ActiveGeneration { get; init; }

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
            if (_closedForExecution || (_borrowedFrom is not null && _borrowedFrom._closedForExecution))
                throw new RouteCatalogUnavailableException("A disposed catalog dispatch scope cannot admit detached work.");
    }

    /// <summary>Enrolls the complete captured scope before any remote factory can run, or refuses it without effects.</summary>
    internal static void EnrollRemote(IReadOnlyList<RouteCatalogDispatchLease> leases)
    {
        lock (_stateGate)
        {
            if (leases.Any(lease => lease._closedForExecution || lease.Root._closedForExecution))
                throw new RouteCatalogUnavailableException("A disposed catalog dispatch scope cannot start remote execution.");
            foreach (var lease in leases)
            {
                lease._remoteCalls++;
                if (lease._borrowedFrom is { } root) root._remoteCalls++;
            }
        }
    }

    /// <summary>Unwinds one enrollment after actual completion without clearing a retained durable fence.</summary>
    internal static void CompleteRemote(IReadOnlyList<RouteCatalogDispatchLease> leases)
    {
        lock (_stateGate)
        {
            foreach (var lease in leases)
            {
                if (--lease._remoteCalls == 0)
                {
                    lease.CompleteClosedSharedReference();
                    lease._remoteDrain?.TrySetResult();
                    lease._remoteDrain = null;
                }
                if (lease._borrowedFrom is { } root && --root._remoteCalls == 0)
                {
                    root.CompleteClosedSharedReference();
                    root._remoteDrain?.TrySetResult();
                    root._remoteDrain = null;
                }
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
        lock (_stateGate)
        {
            RetainCore();
            _borrowedFrom?.RetainCore();
        }
    }

    private void RetainCore()
    {
        _retained = true;
        if (!_sharedReferenceCompleted) ActiveGeneration?.Retain();
    }

    private void CompleteClosedSharedReference()
    {
        if (_disposed && _remoteCalls == 0 && !_sharedReferenceCompleted && ActiveGeneration is { } generation)
        {
            _releaseSharedFence = generation.CompleteReference();
            _sharedReferenceCompleted = true;
        }
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
            if (_borrowedFrom is { } root)
            {
                if (_remoteCalls != 0) root.RetainCore();
                return;
            }
            if (_remoteCalls != 0) RetainCore();
            CompleteClosedSharedReference();
            if (_retained || ActiveGeneration is not null && !_releaseSharedFence) return;
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
