using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// Default implementation of <see cref="IProjectionChangedBroadcaster"/> when SignalR is not configured.
/// Prevents null-check ceremony in callers.
/// </summary>
public class NoOpProjectionChangedBroadcaster : IProjectionChangedBroadcaster {
    /// <inheritdoc/>
    public Task BroadcastChangedAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
