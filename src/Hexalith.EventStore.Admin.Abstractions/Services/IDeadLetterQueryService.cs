using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for querying dead-letter entries (FR78). CQRS-split — reads only.
/// </summary>
public interface IDeadLetterQueryService
{
    /// <summary>
    /// Lists dead-letter entries, optionally filtered by tenant.
    /// </summary>
    /// <param name="tenantId">Optional tenant filter.</param>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <param name="continuationToken">An opaque token for fetching the next page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of dead-letter entries.</returns>
    Task<PagedResult<DeadLetterEntry>> ListDeadLettersAsync(string? tenantId, int count, string? continuationToken, CancellationToken ct = default);
}
