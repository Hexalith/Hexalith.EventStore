using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying consistency check results.
/// </summary>
public interface IConsistencyQueryService {
    /// <summary>
    /// Gets consistency check summaries, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of consistency check summaries.</returns>
    Task<IReadOnlyList<ConsistencyCheckSummary>> GetChecksAsync(string? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Gets the full result of a consistency check including anomaly details.
    /// </summary>
    /// <param name="checkId">The check identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full check result, or null if not found.</returns>
    Task<ConsistencyCheckResult?> GetCheckResultAsync(string checkId, CancellationToken ct = default);
}
