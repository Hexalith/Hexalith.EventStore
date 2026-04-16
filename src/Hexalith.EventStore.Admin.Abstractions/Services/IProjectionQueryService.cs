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
    /// Gets detailed information about a specific projection.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="projectionName">The projection name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The projection detail.</returns>
    Task<ProjectionDetail> GetProjectionDetailAsync(string tenantId, string projectionName, CancellationToken ct = default);
}
