using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Bounds participation in deterministic byte reuse to one closing scope, without retaining authority.</summary>
internal sealed class RouteCatalogDecodeScope : IDisposable
{
    private static readonly AsyncLocal<RouteCatalogDecodeScope?> Active = new();
    private readonly object _gate = new();
    private readonly RouteCatalogValidatedBytesMemo _memo;
    private readonly bool _ownsMemo;
    private readonly RouteCatalogDecodeScope? _prior;
    private int _captureCount;
    private int _cacheHits;
    private bool _closed;

    private RouteCatalogDecodeScope(RouteCatalogValidatedBytesMemo? memo = null)
    {
        _ownsMemo = memo is null;
        _memo = memo ?? new();
        _prior = Active.Value;
        Active.Value = this;
    }

    /// <summary>Gets the current scope, which still must atomically prove it is open before use.</summary>
    internal static RouteCatalogDecodeScope? Current => Active.Value;

    /// <summary>Gets the bounded canonical document count for SDK resource controls.</summary>
    internal int Count { get { lock (_gate) return _closed ? 0 : _memo.Count; } }

    /// <summary>Gets the bounded retained canonical byte count for SDK resource controls.</summary>
    internal int RetainedBytes { get { lock (_gate) return _closed ? 0 : _memo.RetainedBytes; } }

    /// <summary>Gets whether this open scope borrows a still-open memo from its actual owning host.</summary>
    internal bool HasHostMemo { get { lock (_gate) return !_closed && !_ownsMemo && !_memo.IsClosed; } }

    /// <summary>Gets the count of exact input snapshots observed within this still-owned acquisition.</summary>
    internal int CaptureCount { get { lock (_gate) return _captureCount; } }

    /// <summary>Gets the count of complete exact-byte matches, without storing any authority result.</summary>
    internal int CacheHits { get { lock (_gate) return _cacheHits; } }

    /// <summary>Enters an independent scope; nested acquisitions do not share their validation results.</summary>
    internal static RouteCatalogDecodeScope Enter() => new();

    /// <summary>Prevents a separate authority operation from inheriting another host's computational memo.</summary>
    internal static IDisposable? Suppress()
    {
        RouteCatalogDecodeScope? prior = Active.Value;
        if (prior is null) return null;
        Active.Value = null;
        return new RouteCatalogDecodeSuppression(() => Active.Value = prior);
    }

    /// <summary>Enters an independent closing scope using only the exact host's private deterministic byte memo.</summary>
    internal static RouteCatalogDecodeScope EnterWithMemo(RouteCatalogValidatedBytesMemo memo)
    {
        ArgumentNullException.ThrowIfNull(memo);
        return new(memo);
    }

    /// <summary>Snapshots current bytes before comparing the complete owned input with prior validated canonical bytes.</summary>
    /// <param name="input">The current caller-owned document.</param>
    /// <param name="captured">An owned input snapshot, never a retained cache buffer.</param>
    /// <param name="root">The snapshot's actual SHA256.</param>
    /// <param name="validated">Whether the complete exact snapshot was previously validated by this owned byte memo.</param>
    /// <returns>Whether this still-open scope can participate; invalid sizes retain the normal decoder error path.</returns>
    internal bool TryCapture(ReadOnlyMemory<byte> input, out byte[] captured, out string root, out bool validated)
    {
        lock (_gate)
        {
            captured = [];
            root = string.Empty;
            validated = false;
            if (_closed || input.Length is 0 or > RouteCatalogCodec.MaximumByteLength) return false;
            _captureCount++;
            captured = input.ToArray();
            root = Convert.ToHexStringLower(SHA256.HashData(captured));
            validated = _memo.Matches(root, captured);
            if (validated) _cacheHits++;
            return true;
        }
    }

    /// <summary>Retains only private canonical writer output after all original checks, including expected root, succeeded.</summary>
    internal void Remember(string root, byte[] canonical)
    {
        lock (_gate)
        {
            if (!_closed) _memo.Remember(root, canonical);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            if (_ownsMemo) _memo.Dispose();
            _captureCount = 0;
            _cacheHits = 0;
        }
        if (ReferenceEquals(Active.Value, this)) Active.Value = _prior;
    }
}
