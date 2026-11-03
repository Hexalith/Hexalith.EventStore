namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owns bounded canonical bytes whose deterministic document validation already succeeded.</summary>
/// <remarks>Contains no decoded graphs, host, state, ETag, trust, permission, generation admission or lease results.</remarks>
internal sealed class RouteCatalogValidatedBytesMemo : IDisposable
{
    private const int MaximumEntries = 3;
    private const int MaximumRetainedBytes = 8 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _canonical = new(StringComparer.Ordinal);
    private int _retainedBytes;
    private bool _closed;

    /// <summary>Gets whether the owning host or scope has released this memo.</summary>
    internal bool IsClosed { get { lock (_gate) return _closed; } }

    /// <summary>Gets the bounded number of private canonical documents.</summary>
    internal int Count { get { lock (_gate) return _canonical.Count; } }

    /// <summary>Gets the bounded private canonical byte count.</summary>
    internal int RetainedBytes { get { lock (_gate) return _retainedBytes; } }

    /// <summary>Matches the complete freshly owned snapshot, using its actual hash only as an index.</summary>
    internal bool Matches(string root, ReadOnlySpan<byte> captured)
    {
        lock (_gate) return !_closed && _canonical.TryGetValue(root, out byte[]? canonical)
            && captured.SequenceEqual(canonical);
    }

    /// <summary>Takes ownership only of private canonical writer output after every original decoder check succeeded.</summary>
    internal void Remember(string root, byte[] canonical)
    {
        lock (_gate)
        {
            if (_closed || _canonical.ContainsKey(root) || _canonical.Count == MaximumEntries
                || canonical.Length > MaximumRetainedBytes - _retainedBytes) return;
            _canonical.Add(root, canonical);
            _retainedBytes += canonical.Length;
        }
    }

    /// <summary>Clears all owned document bytes and permanently refuses further reuse.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _canonical.Clear();
            _retainedBytes = 0;
        }
    }
}
