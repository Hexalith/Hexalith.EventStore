namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Resolves domain service registrations from the DAPR config store for a given tenant and domain.
/// </summary>
public interface IDomainServiceResolver
{
    /// <summary>
    /// Resolves the domain service registration for the specified tenant and domain.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration, or null if no service is registered.</returns>
    Task<DomainServiceRegistration?> ResolveAsync(string tenantId, string domain, CancellationToken cancellationToken = default);
}
