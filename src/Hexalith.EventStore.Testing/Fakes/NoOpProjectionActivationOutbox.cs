using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Testing.Fakes;

/// <summary>Provides a sidecar-free projection-activation outbox for command-routing tests.</summary>
internal sealed class NoOpProjectionActivationOutbox : IProjectionActivationOutbox {
    /// <summary>Gets the shared no-op outbox instance.</summary>
    public static NoOpProjectionActivationOutbox Instance { get; } = new();

    /// <inheritdoc/>
    public Task EnsureAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ProjectionActivationWorkItem?> GetAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ProjectionActivationWorkItem?>(null);
    }

    /// <inheritdoc/>
    public Task CompleteAsync(
        ProjectionActivationWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionActivationWorkItem>>([]);
    }

    /// <inheritdoc/>
    public Task DeferAsync(
        ProjectionActivationWorkItem workItem,
        DateTimeOffset nextDueUtc,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
