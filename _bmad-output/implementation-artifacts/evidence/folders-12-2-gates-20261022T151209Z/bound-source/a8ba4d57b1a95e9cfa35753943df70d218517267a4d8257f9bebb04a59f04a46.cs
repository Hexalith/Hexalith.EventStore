
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// No-op fallback for <see cref="IProjectionUpdateOrchestrator"/>.
/// Used when projection infrastructure is not fully wired (e.g., manual test construction).
/// Same pattern as <see cref="NoOpProjectionChangedBroadcaster"/>.
/// </summary>
public sealed class NoOpProjectionUpdateOrchestrator : IProjectionUpdateOrchestrator, IProjectionPollerDeliveryGateway {
    /// <inheritdoc/>
    public Task UpdateProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DeliverProjectionAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
