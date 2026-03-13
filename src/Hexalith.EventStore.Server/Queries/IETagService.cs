
namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Abstraction for retrieving the current ETag for a projection+tenant pair.
/// Encapsulates DAPR actor proxy creation, timeout, and fail-open error handling.
/// </summary>
public interface IETagService
{
    /// <summary>
    /// Gets the current ETag for a projection+tenant pair.
    /// Returns null if the ETag has never been set (cold start) or if the ETag actor is unavailable.
    /// </summary>
    /// <param name="projectionType">The projection type (domain name, kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current ETag value, or null.</returns>
    Task<string?> GetCurrentETagAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default);
}
