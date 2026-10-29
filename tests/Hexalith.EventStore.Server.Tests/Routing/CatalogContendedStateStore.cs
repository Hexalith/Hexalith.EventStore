using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class CatalogContendedStateStore(IRouteCatalogStateStore inner) : IRouteCatalogStateStore
{
    public RouteCatalogStateCoordinates? Coordinates => inner.Coordinates;

    public int ConflictsRemaining { get; set; }
    public int ConflictsObserved { get; private set; }

    public Task<(RouteCatalogActivationState State, string ETag)> ReadAsync(CancellationToken cancellationToken)
        => inner.ReadAsync(cancellationToken);

    public Task<bool> CompareExchangeAsync(RouteCatalogActivationState state, string etag, CancellationToken cancellationToken)
    {
        if (ConflictsRemaining > 0)
        {
            ConflictsRemaining--; ConflictsObserved++;
            return Task.FromResult(false);
        }
        return inner.CompareExchangeAsync(state, etag, cancellationToken);
    }
}
