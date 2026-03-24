using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Service interface for operator-level storage management operations.
/// </summary>
public interface IStorageCommandService
{
    /// <summary>
    /// Triggers compaction for a tenant, optionally scoped to a specific domain.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">Optional domain scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> TriggerCompactionAsync(string tenantId, string? domain, CancellationToken ct = default);

    /// <summary>
    /// Creates a snapshot for a specific aggregate.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> CreateSnapshotAsync(string tenantId, string domain, string aggregateId, CancellationToken ct = default);

    /// <summary>
    /// Sets the automatic snapshot policy for an aggregate type.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateType">The aggregate type name.</param>
    /// <param name="intervalEvents">The number of events between automatic snapshots.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> SetSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, int intervalEvents, CancellationToken ct = default);

    /// <summary>
    /// Deletes the automatic snapshot policy for an aggregate type.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="aggregateType">The aggregate type name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> DeleteSnapshotPolicyAsync(string tenantId, string domain, string aggregateType, CancellationToken ct = default);
}
