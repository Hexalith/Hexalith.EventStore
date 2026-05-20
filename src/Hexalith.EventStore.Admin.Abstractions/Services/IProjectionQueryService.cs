using Hexalith.EventStore.Admin.Abstractions.Models.Projections;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying projection status and details (FR73).
/// </summary>
public interface IProjectionQueryService {
    /// <summary>
    /// Lists all projections, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of projection statuses.</returns>
    Task<IReadOnlyList<ProjectionStatus>> ListProjectionsAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information about a specific projection, or <see langword="null"/> when the
    /// projection cannot be located in EventStore or in the Admin read-model fallback.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The projection detail, or <see langword="null"/> when not found.</returns>
    Task<ProjectionDetail?> GetProjectionDetailAsync(string tenantId, string projectionName, CancellationToken ct = default);
}
