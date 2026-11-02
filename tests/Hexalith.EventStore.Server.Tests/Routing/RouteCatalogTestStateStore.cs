using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Serialized durable-cell double; every read materializes bytes and CAS rejects stale writers.</summary>
internal sealed class RouteCatalogTestStateStore : IRouteCatalogStateStore
{
    public RouteCatalogStateCoordinates Coordinates { get; set; } = new("route-catalog", "route-catalog-authority-v1", "http://127.0.0.1:3500/");
    public TimeSpan CompareExchangeLatency { get; set; }

    private readonly object _gate = new();
    private byte[] _state = JsonSerializer.SerializeToUtf8Bytes(RouteCatalogActivationState.Empty);
    private long _version;

    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult((JsonSerializer.Deserialize<RouteCatalogActivationState>(_state)!, _version.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public async Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CompareExchangeLatency > TimeSpan.Zero)
            await Task.Delay(CompareExchangeLatency, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (etag != _version.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            _state = JsonSerializer.SerializeToUtf8Bytes(state);
            _version++;
            return true;
        }
    }
}
