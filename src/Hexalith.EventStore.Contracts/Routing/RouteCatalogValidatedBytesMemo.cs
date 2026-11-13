namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owns bounded canonical bytes whose deterministic document validation already succeeded.</summary>
/// <remarks>Contains no decoded graphs, host, state, ETag, trust, permission, generation admission or lease results.</remarks>
internal sealed class RouteCatalogValidatedBytesMemo : IDisposable
{
    private const int MaximumEntries = 3;
    private const int MaximumRetainedBytes = 8 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<string, (byte[] Bytes, string? Profile)> _canonical = new(StringComparer.Ordinal);
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
        => Matches(root, captured, out _);

    /// <summary>Returns an optional bounded immutable input fact only with complete current canonical-byte equality.</summary>
    internal bool Matches(string root, ReadOnlySpan<byte> captured, out string? profile)
    {
        lock (_gate)
        {
            profile = null;
            if (_closed || !_canonical.TryGetValue(root, out var canonical) || !captured.SequenceEqual(canonical.Bytes)) return false;
            profile = canonical.Profile;
            return true;
        }
    }

    /// <summary>Takes ownership only of private canonical writer output after every original decoder check succeeded.</summary>
    internal void Remember(string root, byte[] canonical, string? profile = null)
    {
        lock (_gate)
        {
            if (_closed) return;
            // Decoder validation bounds Profile tokens to 128 characters. Scalar storage is
            // independently bounded by the three document slots; RetainedBytes still counts canonical bytes only.
            string? fact = profile is { Length: > 0 and <= 128 } ? profile : null;
            if (_canonical.TryGetValue(root, out var existing))
            {
                if (existing.Profile is null && fact is not null && canonical.AsSpan().SequenceEqual(existing.Bytes))
                    _canonical[root] = (existing.Bytes, fact);
                return;
            }
            if (_canonical.Count == MaximumEntries
                || canonical.Length > MaximumRetainedBytes - _retainedBytes) return;
            _canonical.Add(root, (canonical, fact));
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
