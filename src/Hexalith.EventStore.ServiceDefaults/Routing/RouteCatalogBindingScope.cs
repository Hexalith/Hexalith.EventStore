using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Bounds deterministic binding reuse to an open authority operation and its exact owning memo.</summary>
internal sealed class RouteCatalogBindingScope : IDisposable
{
    private static readonly AsyncLocal<RouteCatalogBindingScope?> Active = new();
    private readonly object _gate = new();
    private readonly RouteCatalogBindingMemo _memo;
    private readonly bool _ownsMemo;
    private readonly RouteCatalogBindingScope? _prior;
    private bool _closed;

    private RouteCatalogBindingScope(RouteCatalogBindingMemo? memo)
    {
        _ownsMemo = memo is null;
        _memo = memo ?? new();
        _prior = Active.Value;
        Active.Value = this;
    }

    /// <summary>Gets the current scope; each call must still verify it remains open.</summary>
    internal static RouteCatalogBindingScope? Current => Active.Value;

    /// <summary>Gets the count of complete exact-input matches within this operation.</summary>
    internal int CacheHits { get; private set; }

    /// <summary>Enters a closing scope, optionally borrowing only the owning authority's private memo.</summary>
    internal static RouteCatalogBindingScope Enter(RouteCatalogBindingMemo? memo = null) => new(memo);

    /// <summary>Prevents independent authority operations from inheriting another owner's binding reuse.</summary>
    internal static IDisposable? Suppress()
    {
        var prior = Active.Value;
        if (prior is null) return null;
        Active.Value = null;
        return new RouteCatalogDecodeSuppression(() => Active.Value = prior);
    }

    /// <summary>Reuses deterministic work only while this operation and its storage remain open.</summary>
    internal bool TryCompute(RouteCatalogActivation activation, out string digest)
    {
        lock (_gate)
        {
            digest = string.Empty;
            if (_closed || !_memo.TryCompute(activation, out digest, out bool reused)) return false;
            if (reused) CacheHits++;
            return true;
        }
    }

    /// <summary>Closes participation and clears scope-owned inputs, retaining no authority results.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            if (_ownsMemo) _memo.Dispose();
        }
        if (ReferenceEquals(Active.Value, this)) Active.Value = _prior;
    }
}
