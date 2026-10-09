using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Queries;
/// <summary>Owns one complete private actor root and refuses current-root substitution at completion.</summary>
internal sealed class PrivateLogicalQueryInput : IDisposable
{
    private Func<bool, CancellationToken, Task<DateTimeOffset>>? _fence;
    private Action? _clearEvidence;
    private readonly CancellationToken _originatingToken;
    private readonly EventBufferReservation _charge;
    private readonly HashSet<string> _readKeys = new(StringComparer.Ordinal);
    private DaprLogicalQueryRoot? _root;
    private readonly byte[] _requestHash;
    private readonly byte[] _compatibilityHash;
    /// <summary>Takes an already charged exclusive root and its actual owner readback fence.</summary>
    internal PrivateLogicalQueryInput(DaprLogicalQueryRoot root, EventBufferReservation charge, Func<bool, CancellationToken, Task<DateTimeOffset>> fence, CancellationToken token, byte[] requestHash, ReadOnlySpan<byte> compatibilityHash, Action clearEvidence, EventEvolutionCapabilityLoss loss)
    {
        _requestHash = requestHash;
        _compatibilityHash = compatibilityHash.ToArray();
        _root = root;
        _charge = charge;
        _fence = fence;
        _originatingToken = token;
        _clearEvidence = clearEvidence;
        CapabilityLoss = loss;
    }

    /// <summary>Gets the shared observed-loss scope pinned by actual owner composition.</summary>
    internal EventEvolutionCapabilityLoss CapabilityLoss { get; }
    /// <summary>Gets the exclusive root while its operation remains open.</summary>
    internal DaprLogicalQueryRoot Root => _root ?? throw Closed();

    /// <summary>Requires the exact query, principal metadata and catalog digest admitted by its actual owner.</summary>
    internal void RequireScope(QueryEnvelope query, ReadOnlySpan<byte> compatibilityHash, EventBufferBudget budget, CancellationToken token)
    {
        RequireToken(token);
        _ = Root;
        byte[] current = LogicalQueryRequestCodec.Compute(query, budget, token);
        try
        {
            if (!current.AsSpan().SequenceEqual(_requestHash) || !compatibilityHash.SequenceEqual(_compatibilityHash))
            {
                throw new InvalidOperationException("ReadModelRouteContextRequired: foreign query/catalog scope.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(current);
        }
    }

    /// <summary>Rechecks actual current authority, optionally including exact current-root readback.</summary>
    internal async Task RequireCurrentAsync(bool compareRoot, CancellationToken token)
    {
        RequireToken(token);
        _ = Root;
        DateTimeOffset now;
        Func<bool, CancellationToken, Task<DateTimeOffset>> fence = _fence ?? throw Closed();
        try
        {
            now = await fence(compareRoot, token).ConfigureAwait(false);
        }
        finally
        {
            _originatingToken.ThrowIfCancellationRequested();
        }

        RequireToken(token);
        foreach (DaprLogicalQueryRow row in Root.Rows)
        {
            if (_readKeys.Contains(row.Key) && row.ExpiresAt is { } expiry && now >= expiry)
            {
                throw new InvalidOperationException("ReadModelExpiryPending: a pinned row expired.");
            }
        }
    }

    /// <summary>Returns only a pre-admitted exact logical row after registering its final expiry obligation.</summary>
    internal DaprLogicalQueryRow RequireRow(string store, string key, CancellationToken token)
    {
        RequireToken(token);
        DaprLogicalQueryRoot root = Root;
        if (store != root.StoreName)
        {
            throw Closed();
        }

        DaprLogicalQueryRow row = root.Rows.SingleOrDefault(x => x.Key == key) ?? throw new InvalidOperationException("ReadModelQueryConsistencyHold: absence has no originating witness.");
        _readKeys.Add(key);
        return row;
    }

    /// <summary>Requires exact originating token propagation, before closed-context or provider refusal.</summary>
    internal void RequireToken(CancellationToken token)
    {
        _originatingToken.ThrowIfCancellationRequested();
        CapabilityLoss.RequireNoObservedLoss();
        if (token != _originatingToken)
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: foreign cancellation scope.");
        }

        token.ThrowIfCancellationRequested();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DaprLogicalQueryRoot? root = Interlocked.Exchange(ref _root, null);
        if (root is null)
        {
            return;
        }

        foreach (DaprLogicalQueryRow row in root.Rows)
        {
            if (row.Payload is not null)
            {
                CryptographicOperations.ZeroMemory(row.Payload);
            }
        }

        Array.Clear(root.Rows);
        CryptographicOperations.ZeroMemory(root.BackendDescriptor);
        _readKeys.Clear();
        CryptographicOperations.ZeroMemory(_compatibilityHash);
        Interlocked.Exchange(ref _fence, null);
        Interlocked.Exchange(ref _clearEvidence, null)?.Invoke();
        _charge.Dispose();
    }

    private static InvalidOperationException Closed() => new("ReadModelRouteContextRequired: query input is unavailable or closed.");
}
