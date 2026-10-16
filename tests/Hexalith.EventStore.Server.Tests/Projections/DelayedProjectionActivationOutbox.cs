using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Delays only exact-revision completion while all persistence remains in the owning outbox.</summary>
internal sealed class DelayedProjectionActivationOutbox(IProjectionActivationOutbox inner) : IProjectionActivationOutbox
{
    internal Func<CancellationToken, Task>? BeforeCompleteAsync { get; set; }

    public Task EnsureAsync(AggregateIdentity identity, CancellationToken token = default) => inner.EnsureAsync(identity, token);
    public Task<ProjectionActivationWorkItem?> GetAsync(AggregateIdentity identity, CancellationToken token = default) => inner.GetAsync(identity, token);
    public Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(DateTimeOffset due, int count, CancellationToken token = default)
        => inner.GetDueAsync(due, count, token);
    public Task DeferAsync(ProjectionActivationWorkItem work, DateTimeOffset due, CancellationToken token = default) => inner.DeferAsync(work, due, token);
    public async Task CompleteAsync(ProjectionActivationWorkItem work, CancellationToken token = default)
    {
        if (BeforeCompleteAsync is { } before) await before(token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await inner.CompleteAsync(work, token).ConfigureAwait(false);
    }
}
