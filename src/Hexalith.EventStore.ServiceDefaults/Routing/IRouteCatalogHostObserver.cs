using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Captures actual registrations and loaded topology independently of a proposed catalog; configured fingerprints alone never attest readiness.</summary>
public interface IRouteCatalogHostObserver
{
    /// <summary>Validates the complete local inventory and returns its independently calculated identity.</summary>
    Task<RouteCatalogHostIdentity> ObserveAsync(RouteCatalogActivation activation, RouteCatalogStateCoordinates coordinates, CancellationToken cancellationToken);
}
