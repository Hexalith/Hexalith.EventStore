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
}
