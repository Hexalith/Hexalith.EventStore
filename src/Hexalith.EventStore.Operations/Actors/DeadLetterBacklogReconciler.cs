using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Operations.Configuration;
using Hexalith.EventStore.Operations.Models;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Actors;

/// <summary>
/// Activates the drain actor once after startup so the backlog gauges reflect retained work.
/// </summary>
/// <remarks>
/// The gauges are refreshed from the actor, and the actor is only activated by a capture, an operator call, or a
/// replay reminder. A host that restarts holding a captured-but-never-retried backlog has none of those: no
/// reminder is armed for a purely pending item, and nothing else touches the actor. Without this reconciliation
/// the backlog-count and oldest-age alerts would read zero for exactly the backlog nobody is watching. Reading a
/// single-item page is enough -- activation itself is what publishes the observation.
/// </remarks>
internal sealed class DeadLetterBacklogReconciler(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreOperationsOptions> options) : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EventStoreOperationsOptions value = options.Value;
        for (int attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                IDeadLetterDrainActor actor = actorProxyFactory.CreateActorProxy<IDeadLetterDrainActor>(
                    new ActorId(value.TopicName),
                    DeadLetterDrainActor.ActorTypeName);
                _ = await actor
                    .ListAsync(new DeadLetterListRequest(null, 1, 0))
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The sidecar and the actor placement table are not necessarily ready when the host starts. No
                // identifier or payload is available here to log, and a failed reconciliation degrades only the
                // gauges, so the attempt is retried a bounded number of times and then abandoned.
            }

            try
            {
                await Task.Delay(_retryDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
