using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Tracks a host's overlapping dispatch lifetimes under one durable generation fence.</summary>
internal sealed class RouteCatalogActiveGenerationLease(RouteCatalogDispatchFence fence, RouteCatalogActivation activation,
    string bindingDigest, RouteCatalogHostIdentity host, RouteCatalogStateCoordinates? coordinates,
    Action<RouteCatalogActiveGenerationLease> onDrained)
{
    private readonly object _gate = new();
    private int _references = 1;
    private bool _retained;

    /// <summary>Gets the exact durable fence for this group.</summary>
    internal RouteCatalogDispatchFence Fence => fence;

    /// <summary>Gets the privately owned admission candidate used to seed fresh validation.</summary>
    internal RouteCatalogActivation Activation => activation;

    /// <summary>Gets the validated binding of the group's immutable activation.</summary>
    internal string BindingDigest => bindingDigest;

    /// <summary>Checks whether all local references, including enrolled remote work, have drained.</summary>
    internal bool HasReferences
    {
        get { lock (_gate) return _references != 0; }
    }

    /// <summary>Checks whether this group may accept new work from the exact same local host.</summary>
    internal bool CanJoin(RouteCatalogHostIdentity identity, RouteCatalogStateCoordinates? currentCoordinates)
    {
        lock (_gate) return _references != 0 && !_retained && host == identity && coordinates == currentCoordinates;
    }

    /// <summary>Reserves an admission before its strong read so reconciliation proof covers in-flight admissions.</summary>
    internal bool TryReserveReference()
    {
        lock (_gate)
        {
            if (_references == 0 || _retained) return false;
            _references = checked(_references + 1);
            return true;
        }
    }

    /// <summary>Requires a fresh, unique, exact and unsealed durable fence.</summary>
    internal void RequireFence(RouteCatalogActivationState state)
    {
        if (fence.Sequence > state.LastLease || !state.Leases.Contains(fence)
            || state.Leases.Count(item => item.LeaseId == fence.LeaseId) != 1)
            throw new RouteCatalogUnavailableException("The active generation dispatch fence differs from the validated authority.");
    }

    /// <summary>Seals this group and retains its durable fence for independently verified reconciliation.</summary>
    internal void Retain()
    {
        lock (_gate) _retained = true;
    }

    /// <summary>Drops a local reference and grants durable release to the final unambiguous completion.</summary>
    internal bool CompleteReference()
    {
        bool release;
        lock (_gate)
        {
            if (--_references != 0) return false;
            release = !_retained;
        }
        onDrained(this);
        return release;
    }
}
