using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class NoOpProjectionActivationOutbox : IProjectionActivationOutbox
{
    public Task EnsureAsync(AggregateIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<ProjectionActivationWorkItem?> GetAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ProjectionActivationWorkItem?>(null);
    }

    public Task CompleteAsync(
        ProjectionActivationWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionActivationWorkItem>>([]);
    }

    public Task DeferAsync(
        ProjectionActivationWorkItem workItem,
        DateTimeOffset nextDueUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
