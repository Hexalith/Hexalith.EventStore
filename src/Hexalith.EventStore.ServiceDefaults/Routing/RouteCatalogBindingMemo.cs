using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Retains only successful deterministic binding computations over bounded private graphs.</summary>
internal sealed class RouteCatalogBindingMemo : IDisposable
{
    private const int MaximumEntries = 3;
    private const int MaximumBytes = 8 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly List<(RouteCatalogBindingInput Input, string Digest)> _entries = [];
    private int _bytes;
    private bool _closed;

    /// <summary>Gets whether the owning authority permanently released its storage.</summary>
    internal bool IsClosed { get { lock (_gate) return _closed; } }

    /// <summary>Gets the retained computation count.</summary>
    internal int Count { get { lock (_gate) return _entries.Count; } }

    /// <summary>Gets the conservative size of retained private inputs.</summary>
    internal int RetainedBytes { get { lock (_gate) return _bytes; } }

    /// <summary>Captures all current deterministic inputs before comparing or computing; failed validation is never remembered.</summary>
    internal bool TryCompute(RouteCatalogActivation activation, out string digest, out bool reused)
    {
        lock (_gate)
        {
            digest = string.Empty;
            reused = false;
            if (_closed || !RouteCatalogBindingInput.TryCapture(activation, out var captured)) return false;
            foreach (var entry in _entries)
            {
                if (!entry.Input.Matches(captured!)) continue;
                digest = entry.Digest;
                reused = true;
                return true;
            }
            digest = RouteCatalogActivationBinding.ComputeDigestCore(captured!.Activation);
            if (_entries.Count < MaximumEntries && captured.Bytes <= MaximumBytes - _bytes)
            {
                _entries.Add((captured, digest));
                _bytes += captured.Bytes;
            }
            return true;
        }
    }

    /// <summary>Clears all private inputs without affecting durable authority or active leases.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _entries.Clear();
            _bytes = 0;
        }
    }
}
