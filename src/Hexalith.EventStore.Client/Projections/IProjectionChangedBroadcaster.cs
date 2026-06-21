namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Broadcasts a signal-only "changed" message to subscribed real-time clients.
/// Implementations: SignalR (real-time push), No-op (disabled).
/// </summary>
public interface IProjectionChangedBroadcaster {
    /// <summary>
    /// Broadcasts a projection change signal to all clients subscribed to
    /// the group {projectionType}:{tenantId}.
    /// </summary>
    /// <param name="projectionType">The projection type name (kebab-case).</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastChangedAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a metadata-rich, optionally scoped projection change to subscribed clients.
    /// <para>
    /// The broadcast targets the group <c>{ProjectionType}:{TenantId}</c> when
    /// <see cref="ProjectionChangedDetail.GroupScope"/> is null/empty, or
    /// <c>{ProjectionType}:{TenantId}:{GroupScope}</c> when a scope is present — so a scoped
    /// broadcast reaches only the matching scoped subscribers, not every tenant-wide watcher.
    /// </para>
    /// <para>
    /// <see cref="ProjectionChangedDetail.Metadata"/> is opaque to the framework; implementations
    /// MUST bound its size and MUST NOT log metadata values above <c>Debug</c> level.
    /// </para>
    /// </summary>
    /// <param name="detail">The opaque, metadata-only, optionally scoped change detail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastChangedAsync(
        ProjectionChangedDetail detail,
        CancellationToken cancellationToken = default);
}
