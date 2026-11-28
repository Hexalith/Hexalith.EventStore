using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Owner-supplied verification of actual remote quiescence, bound to one exact persisted dispatch. No default verifier grants authority.</summary>
public interface IRouteCatalogQuiescenceVerifier
{
    /// <summary>Throws unless trusted owner evidence proves all work covered by this fence has stopped.
    /// A shared fence covers every local reference and its enrolled remote work. Proof is
    /// verified again after durable sealing and on CAS retries.</summary>
    Task VerifyAsync(RouteCatalogQuiescenceRequest request, RouteCatalogDispatchFence fence, CancellationToken cancellationToken);
}
