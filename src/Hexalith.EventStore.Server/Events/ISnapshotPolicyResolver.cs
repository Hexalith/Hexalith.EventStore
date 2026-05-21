namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Resolves persisted automatic snapshot policy intervals.
/// </summary>
public interface ISnapshotPolicyResolver {
    /// <summary>
    /// Gets a persisted policy interval for the exact tenant/domain/aggregate type tuple.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateType">Aggregate type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured interval, or <c>null</c> when no persisted policy exists.</returns>
    Task<int?> GetIntervalAsync(
        string tenantId,
        string domain,
        string aggregateType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates a cached exact policy decision after a mutation.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateType">Aggregate type name.</param>
    void Invalidate(string tenantId, string domain, string aggregateType);
}
