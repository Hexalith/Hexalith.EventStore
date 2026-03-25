using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for consistency check operations.
/// </summary>
public interface IConsistencyCommandService
{
    /// <summary>
    /// Triggers a new consistency check for the specified scope.
    /// </summary>
    /// <param name="tenantId">Optional tenant scope (null for all tenants).</param>
    /// <param name="domain">Optional domain scope (null for all domains).</param>
    /// <param name="checkTypes">Types of consistency checks to perform.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result with the check ID.</returns>
    Task<AdminOperationResult> TriggerCheckAsync(string? tenantId, string? domain, IReadOnlyList<ConsistencyCheckType> checkTypes, CancellationToken ct = default);

    /// <summary>
    /// Cancels a running consistency check.
    /// </summary>
    /// <param name="checkId">The check identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> CancelCheckAsync(string checkId, CancellationToken ct = default);
}
