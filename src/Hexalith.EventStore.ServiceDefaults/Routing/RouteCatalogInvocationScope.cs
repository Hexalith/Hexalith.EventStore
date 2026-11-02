namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Shares one durable catalog fence across the ordered native source observations of one request.</summary>
public sealed class RouteCatalogInvocationScope(IRouteCatalogAuthority authority)
{
    private readonly AsyncLocal<RouteCatalogNativeReadSession?> _active = new();

    /// <summary>Executes one complete workload stream-read sequence under a single durable catalog fence.</summary>
    public async Task<T> ExecuteNativeSourceReadAsync<T>(string targetAppId, Func<Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetAppId);
        ArgumentNullException.ThrowIfNull(read);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active.Value is not null)
            throw new RouteCatalogUnavailableException("A native source read scope cannot be nested.");
        RouteCatalogDispatchLease lease = await authority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        if (lease.Activation is null)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw new RouteCatalogUnavailableException("A native source read requires an owner-acquired catalog lease.");
        }
        var session = new RouteCatalogNativeReadSession(authority, lease, targetAppId);
        _active.Value = session;
        try
        {
            return await read().ConfigureAwait(false);
        }
        finally
        {
            session.Close();
            _active.Value = null;
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Only the exact workload stream-read route may reuse the enrolled owner lease.</summary>
    internal RouteCatalogDispatchLease? LeaseFor(IRouteCatalogAuthority caller, string appId, string method, HttpMethod httpMethod)
    {
        RouteCatalogNativeReadSession? session = _active.Value;
        if (session is null || !ReferenceEquals(session.Authority, caller)
            || !string.Equals(session.TargetAppId, appId, StringComparison.Ordinal)
            || !string.Equals(method, "api/v1/streams/read/workload", StringComparison.Ordinal)
            || httpMethod != HttpMethod.Post)
            return null;
        session.RequireOpen();
        return session.Lease;
    }
}
