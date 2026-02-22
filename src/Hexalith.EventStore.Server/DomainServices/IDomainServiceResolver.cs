namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Resolves domain service registrations from the DAPR config store for a given tenant and domain.
/// </summary>
public interface IDomainServiceResolver {
    /// <summary>
    /// Resolves the domain service registration for the specified tenant, domain, and version.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="version">The service version (e.g., "v1"). Defaults to "v1".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration, or null if no service is registered.</returns>
    Task<DomainServiceRegistration?> ResolveAsync(string tenantId, string domain, string version = "v1", CancellationToken cancellationToken = default);
}
