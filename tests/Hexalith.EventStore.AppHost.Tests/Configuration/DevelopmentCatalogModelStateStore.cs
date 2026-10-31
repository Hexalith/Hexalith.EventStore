using System.Globalization;
using System.Text.Json;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Serialized CAS test cell for model-input activation checks; it never contacts Redis or starts a process.</summary>
internal sealed class DevelopmentCatalogModelStateStore(RouteCatalogStateCoordinates coordinates) : IRouteCatalogStateStore
{
    private byte[] _state = JsonSerializer.SerializeToUtf8Bytes(RouteCatalogActivationState.Empty);
    private long _version;
    public RouteCatalogStateCoordinates Coordinates { get; } = coordinates;
    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((JsonSerializer.Deserialize<RouteCatalogActivationState>(_state)!, _version.ToString(CultureInfo.InvariantCulture)));
    }
    public Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (etag != _version.ToString(CultureInfo.InvariantCulture)) return Task.FromResult(false);
        _state = JsonSerializer.SerializeToUtf8Bytes(state); _version++; return Task.FromResult(true);
    }
}
