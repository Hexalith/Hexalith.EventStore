using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owner-supplied verification of actual remote quiescence, bound to one exact persisted dispatch. No default verifier grants authority.</summary>
public interface IRouteCatalogQuiescenceVerifier
{
    /// <summary>Throws unless the owner's trusted evidence proves all work of this dispatch has stopped. Must support repeat validation during CAS retries.</summary>
    Task VerifyAsync(RouteCatalogQuiescenceRequest request, RouteCatalogDispatchFence fence, CancellationToken cancellationToken);
}
