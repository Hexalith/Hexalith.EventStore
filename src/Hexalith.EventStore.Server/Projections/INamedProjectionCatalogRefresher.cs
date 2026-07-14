using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Refreshes one resolver-aligned named projection catalog binding on demand.</summary>
public interface INamedProjectionCatalogRefresher {
    /// <summary>Loads and verifies the exact app/version/domain binding.</summary>
    /// <param name="registration">The registration returned by the authoritative resolver.</param>
    /// <param name="cancellationToken">Propagates refresh cancellation.</param>
    /// <returns><c>true</c> when a verified binding was published.</returns>
    Task<bool> RefreshAsync(
        DomainServiceRegistration registration,
        CancellationToken cancellationToken = default);
}
