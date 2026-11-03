namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owns one bounded metadata request and links its actual receiver scopes without owning their durable leases.</summary>
internal sealed class RouteCatalogHeldMetadataDispatch : IDisposable
{
    private readonly object _issuer;
    private readonly RouteCatalogRemoteScope[] _scopes;
    private readonly RouteCatalogDispatchLease[] _leases;
    private readonly CancellationTokenSource _cancellation;
    private bool _disposed;
    private int _invoked;

    /// <summary>Captures the genuine receiver layers and their original tokens under the shared lifetime gate.</summary>
    internal RouteCatalogHeldMetadataDispatch(object issuer, RouteCatalogRemoteScope[] scopes, CancellationToken caller)
    {
        _issuer = issuer;
        _scopes = scopes.ToArray();
        lock (RouteCatalogDispatchLease.MetadataLifetimeGate)
        {
            foreach (var scope in _scopes) scope.RequireMetadataLifetime(issuer);
            _leases = scopes.Select(scope => scope.Lease).Distinct().ToArray();
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource([caller, .. scopes.SelectMany(scope => scope.MetadataTokens)]);
            Lease = scopes[^1].Lease;
            try { RequireOpen(); }
            catch { _cancellation.Dispose(); throw; }
        }
    }

    /// <summary>Gets the actual original receiver lease; this handle never disposes it.</summary>
    internal RouteCatalogDispatchLease Lease { get; }

    /// <summary>Gets its deeply frozen admitted catalog snapshot.</summary>
    internal CommittedRouteCatalogSnapshot Snapshot => Lease.Snapshot;

    /// <summary>Gets a fresh private copy of the issuer's activation bytes.</summary>
    internal byte[] Activation => Lease.GetMetadataActivation(_issuer);

    /// <summary>Gets linked caller, original receiver and original scope-closure cancellation.</summary>
    internal CancellationToken Token => _cancellation.Token;

    /// <summary>Gets or sets the privately captured immutable metadata frame before validation.</summary>
    internal HttpRequestMessage? Request { get; set; }

    /// <summary>Requires every layer to share the exact local issuer, generation and authority coordinates.</summary>
    internal bool MatchesLocal(string? appId, RouteCatalogStateCoordinates? coordinates)
        => Snapshot.LocalAppId == appId && Snapshot.AuthorityCoordinates == coordinates
            && _leases.All(lease => lease.Snapshot.RootDigest == Snapshot.RootDigest && lease.Snapshot.Epoch == Snapshot.Epoch
                && lease.Snapshot.ActivationBindingDigest == Snapshot.ActivationBindingDigest
                && lease.Snapshot.LocalAppId == Snapshot.LocalAppId && lease.Snapshot.AuthorityCoordinates == Snapshot.AuthorityCoordinates);

    /// <summary>Refuses cancellation and closure in any captured layer.</summary>
    internal void RequireOpen()
    {
        lock (RouteCatalogDispatchLease.MetadataLifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Token.ThrowIfCancellationRequested();
            foreach (var scope in _scopes) scope.RequireMetadataLifetime(_issuer);
        }
    }

    /// <summary>Atomically admits real remote work, retaining all actual ancestors on ambiguity or a late result.</summary>
    internal async Task<HttpResponseMessage> InvokeAsync(Func<CancellationToken, Task<HttpResponseMessage>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        if (Interlocked.Exchange(ref _invoked, 1) != 0)
            throw new RouteCatalogUnavailableException("Each metadata send requires its own fresh validation.");
        lock (RouteCatalogDispatchLease.MetadataLifetimeGate)
        {
            RequireOpen();
            RouteCatalogDispatchLease.EnrollRemote(_leases);
        }
        bool started = false;
        try
        {
            RequireOpen();
            started = true;
            HttpResponseMessage response = await invoke(Token).ConfigureAwait(false);
            try { RequireOpen(); }
            catch { response.Dispose(); throw; }
            return response;
        }
        catch
        {
            if (started) foreach (var lease in _leases) lease.Retain();
            throw;
        }
        finally { RouteCatalogDispatchLease.CompleteRemote(_leases); }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        Request?.Dispose();
        _cancellation.Dispose();
    }
}
