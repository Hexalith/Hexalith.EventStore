using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Distributed activation and dispatch fence over one complete catalog.</summary>
public interface IRouteCatalogAuthority
{
    /// <summary>Prepares a validated immutable candidate and fences new requests.</summary>
    Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default);
    /// <summary>Attests only the independently configured local host after prior dispatch has drained.</summary>
    Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default);
    /// <summary>Commits only after every exact required host is ready and no old dispatch remains.</summary>
    Task CommitAsync(string attemptId, CancellationToken cancellationToken = default);
    /// <summary>Aborts a pending attempt or rolls back to the retained complete predecessor.</summary>
    Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default);
    /// <summary>Releases only one exact fence after an owner verifier proves actual remote quiescence; defaults to refusal.</summary>
    Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default);
    /// <summary>Acquires a durable dispatch lease and a matching committed snapshot.</summary>
    Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default);
    /// <summary>Reads shared Dapr authority and validates local agreement.</summary>
    Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default);
}
