using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying system health and DAPR component status (FR75).
/// </summary>
public interface IHealthQueryService
{
    /// <summary>
    /// Gets the overall system health report.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The system health report.</returns>
    Task<SystemHealthReport> GetSystemHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the health status of all DAPR components.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of DAPR component health statuses.</returns>
    Task<IReadOnlyList<DaprComponentHealth>> GetDaprComponentStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets DAPR component health history for a time range, with optional component filtering.
    /// </summary>
    /// <param name="from">Start of the time range.</param>
    /// <param name="to">End of the time range.</param>
    /// <param name="componentName">Optional component name filter (case-insensitive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A timeline of health history entries.</returns>
    Task<DaprComponentHealthTimeline> GetComponentHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? componentName,
        CancellationToken ct = default);
}
