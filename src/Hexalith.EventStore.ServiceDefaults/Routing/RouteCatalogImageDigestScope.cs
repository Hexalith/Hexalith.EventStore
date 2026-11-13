using System.Reflection;

using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Permits exact fresh-image computation reuse only during an open operation of the owning authority.</summary>
internal sealed class RouteCatalogImageDigestScope : IDisposable
{
    private static readonly AsyncLocal<RouteCatalogImageDigestScope?> Active = new();
    private readonly object _gate = new();
    private readonly RouteCatalogImageDigestMemo _memo;
    private readonly bool _ownsMemo;
    private readonly RouteCatalogImageDigestScope? _prior;
    private bool _closed;

    private RouteCatalogImageDigestScope(RouteCatalogImageDigestMemo? memo)
    {
        _ownsMemo = memo is null;
        _memo = memo ?? new();
        _prior = Active.Value;
        Active.Value = this;
    }

    /// <summary>Gets the current scope, which must still prove it is open for each image computation.</summary>
    internal static RouteCatalogImageDigestScope? Current => Active.Value;

    /// <summary>Gets whether this scope can begin bounded capture, without granting admission or replacing a fresh read.</summary>
    internal bool IsOpen { get { lock (_gate) return !_closed && !_memo.IsClosed; } }

    /// <summary>Gets the number of complete fresh-byte matches within this operation.</summary>
    internal int CacheHits { get; private set; }

    /// <summary>Enters independent scoped storage or borrows only the exact supplied host memo.</summary>
    internal static RouteCatalogImageDigestScope Enter(RouteCatalogImageDigestMemo? memo = null) => new(memo);

    /// <summary>Prevents another authority or lifecycle operation from inheriting retained image computations.</summary>
    internal static IDisposable? Suppress()
    {
        var prior = Active.Value;
        if (prior is null) return null;
        Active.Value = null;
        return new RouteCatalogDecodeSuppression(() => Active.Value = prior);
    }

    /// <summary>Compares the complete fresh SDK-owned image while its original bounded read lifetime remains open.</summary>
    internal bool TryCompute(Assembly assembly, ReadOnlySpan<byte> current, out string digest)
    {
        lock (_gate)
        {
            digest = string.Empty;
            if (_closed || !_memo.TryCompute(assembly, current, out digest, out bool reused)) return false;
            if (reused) CacheHits++;
            return true;
        }
    }

    /// <summary>Closes all flowed participation and releases scope-owned bytes without retaining authority results.</summary>
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
