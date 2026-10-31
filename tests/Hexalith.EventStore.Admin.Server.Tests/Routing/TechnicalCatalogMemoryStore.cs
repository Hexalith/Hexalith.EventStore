using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

/// <summary>Atomic disposable persistence seam; the authority, trust, observer and lease remain SDK implementations.</summary>
internal sealed class TechnicalCatalogMemoryStore : IRouteCatalogStateStore
{
    private readonly object _gate = new();
    private long _etag;
    private RouteCatalogActivationState _state = RouteCatalogActivationState.Empty;
    public RouteCatalogStateCoordinates Coordinates { get; set; } = new("route-catalog", "admin-technical-test", "http://127.0.0.1:3500/");
    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult((_state, _etag.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
    public Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (etag != _etag.ToString(System.Globalization.CultureInfo.InvariantCulture)) return Task.FromResult(false);
            _state = state; _etag++; return Task.FromResult(true);
        }
    }
}
