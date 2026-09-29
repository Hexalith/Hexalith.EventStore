using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Invokes the configured reminder actor type through the Dapr actor runtime.</summary>
internal sealed class DaprReminderActorInvoker(
    IActorProxyFactory proxyFactory,
    IOptions<EventStoreReminderOptions> options) : IReminderActorInvoker
{
    /// <inheritdoc/>
    public Task<ReminderConvergenceResult> ConvergeAsync(
        string actorId,
        ReminderTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(target);

        // Dapr actor remoting methods carry no cancellation token; honour it at the boundary. Convergence is
        // idempotent, so abandoning a cancelled call before the proxy is safe.
        cancellationToken.ThrowIfCancellationRequested();
        IReminderActor actor = proxyFactory.CreateActorProxy<IReminderActor>(
            new ActorId(actorId),
            options.Value.ActorTypeName);
        return actor.ConvergeAsync(target);
    }
}
