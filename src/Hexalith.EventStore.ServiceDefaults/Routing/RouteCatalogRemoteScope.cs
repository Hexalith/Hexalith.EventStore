using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Restores the enclosing RPC boundary without disposing its receiver lease.</summary>
internal sealed class RouteCatalogRemoteScope : IDisposable
{
    private readonly IRouteCatalogRemoteBoundary? _priorBoundary;
    private readonly IReadOnlyList<RouteCatalogDispatchLease>? _prior;
    private readonly IReadOnlyList<RouteCatalogRemoteScope>? _priorMetadataScopes;
    private int _closed;
    private readonly CancellationToken? _receiverToken;
    private readonly CancellationTokenSource? _closure;

    /// <summary>Enters an ordinary or isolated SDK scope and tracks an explicitly issued private lifetime.</summary>
    internal RouteCatalogRemoteScope(RouteCatalogDispatchLease lease, bool isolated = false, CancellationToken? receiverToken = null)
    {
        Lease = lease;
        _receiverToken = receiverToken;
        if (lease.HasMetadataIssuer) _closure = new();
        _priorBoundary = RouteCatalogRemoteExecution.Current;
        _prior = RouteCatalogRemoteCall.Push(lease, isolated);
        _priorMetadataScopes = RouteCatalogRemoteCall.PushMetadataScope(this, isolated);
    }

    /// <summary>Gets the actual lease held by this scope.</summary>
    internal RouteCatalogDispatchLease Lease { get; }

    /// <summary>Gets whether the original scope has closed in any captured execution context.</summary>
    internal bool IsClosed => Volatile.Read(ref _closed) != 0;

    /// <summary>Gets whether a trusted SDK receiver minted this token-bound lifetime.</summary>
    internal bool IsReceiver => _receiverToken is not null;

    /// <summary>Gets the original receiver cancellation and private closure tokens.</summary>
    internal IEnumerable<CancellationToken> MetadataTokens
        => _receiverToken is { } receiver ? [receiver, _closure!.Token] : [_closure!.Token];

    /// <summary>Checks closure, issuer and original receiver cancellation under the shared lifetime gate.</summary>
    internal void RequireMetadataLifetime(object issuer)
    {
        if (IsClosed || !IsReceiver)
            throw new RouteCatalogUnavailableException("Nested metadata requires a live trusted SDK receiver scope.");
        _receiverToken!.Value.ThrowIfCancellationRequested();
        Lease.RequireMetadataIssuer(issuer);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        bool closing;
        lock (RouteCatalogDispatchLease.MetadataLifetimeGate)
        {
            closing = _closed == 0;
            Volatile.Write(ref _closed, 1);
        }
        if (!closing) return;
        try { _closure?.Cancel(); }
        finally
        {
            try { _closure?.Dispose(); }
            finally
            {
                RouteCatalogRemoteCall.RestoreMetadataScopes(_priorMetadataScopes);
                RouteCatalogRemoteCall.Restore(_prior);
                RouteCatalogRemoteExecution.Current = _priorBoundary;
            }
        }
    }
}
