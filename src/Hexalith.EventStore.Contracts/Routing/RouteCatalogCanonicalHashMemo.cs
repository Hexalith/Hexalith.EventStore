namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owns bounded complete serialized-preimage strings and their pure canonical SHA results.</summary>
/// <remarks>Retains no graphs, authority, configuration, admission or lease results.</remarks>
internal sealed class RouteCatalogCanonicalHashMemo : IDisposable
{
    /// <summary>The maximum independently retained serialized preimages.</summary>
    internal const int MaximumEntries = 512;

    /// <summary>The maximum retained charge for UTF-16 payloads and fixed entry allowances.</summary>
    internal const int MaximumRetainedCharge = 4 * 1024 * 1024;

    private const int EntryOverheadCharge = 128;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _digests = new(StringComparer.Ordinal);
    private int _retainedCharge;
    private bool _closed;

    /// <summary>Gets whether the actual owner permanently released this memo.</summary>
    internal bool IsClosed { get { lock (_gate) return _closed; } }

    /// <summary>Gets the independently bounded retained preimage count.</summary>
    internal int Count { get { lock (_gate) return _digests.Count; } }

    /// <summary>Gets retained UTF-16 payloads plus fixed allowances, not measured CLR heap usage.</summary>
    internal int RetainedCharge { get { lock (_gate) return _retainedCharge; } }

    /// <summary>Gets successful complete ordinal matches while the owner remained open.</summary>
    internal int Hits { get; private set; }

    /// <summary>Gets complete preimages absent from the still-open owner.</summary>
    internal int Misses { get; private set; }

    /// <summary>Matches the entire current serialized preimage, without trusting a caller digest.</summary>
    /// <param name="ownedKey">A privately owned complete serialization.</param>
    /// <param name="digest">The pure canonical digest returned only on a full ordinal match.</param>
    /// <returns>Whether the still-open owner retained exactly this preimage.</returns>
    internal bool TryGet(string ownedKey, out string digest)
    {
        lock (_gate)
        {
            digest = string.Empty;
            if (_closed)
            {
                return false;
            }
            if (_digests.TryGetValue(ownedKey, out string? found))
            {
                Hits++;
                digest = found;
                return true;
            }
            Misses++;
            return false;
        }
    }

    /// <summary>Retains a successful computation from the same owned preimage within separate bounds.</summary>
    /// <param name="ownedKey">The complete immutable serialization used for the computation.</param>
    /// <param name="digest">The canonical digest of that same serialization.</param>
    internal void Remember(string ownedKey, string digest)
    {
        lock (_gate)
        {
            if (_closed || _digests.Count >= MaximumEntries || _digests.ContainsKey(ownedKey))
            {
                return;
            }
            // Charge UTF-16 payloads, digest and a conservative fixed per-entry allowance.
            // This is a retained-charge policy, not a claim to measure CLR heap object overhead.
            long charge = EntryOverheadCharge + 2L * (ownedKey.Length + digest.Length);
            if (charge > MaximumRetainedCharge - _retainedCharge)
            {
                return;
            }
            _digests.Add(ownedKey, digest);
            _retainedCharge += (int)charge;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _closed = true;
            _digests.Clear();
            _retainedCharge = 0;
            Hits = 0;
            Misses = 0;
        }
    }
}
