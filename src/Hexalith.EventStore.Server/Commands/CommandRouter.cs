namespace Hexalith.EventStore.Server.Commands;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Logging;

/// <summary>
/// Routes commands to the correct aggregate actor based on canonical identity derivation.
/// SECURITY: Always derive actor ID from AggregateIdentity.ActorId. Never construct actor IDs
/// via manual string concatenation. The chain of custody from CommandEnvelope through
/// AggregateIdentity to actor ID must be unbroken. See FR15, FR28.
/// </summary>
public class CommandRouter(
    IActorProxyFactory actorProxyFactory,
    ILogger<CommandRouter> logger) : ICommandRouter
{
    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;

        logger.LogDebug(
            "Routing command to actor: CorrelationId={CorrelationId}, ActorId={ActorId}",
            command.CorrelationId,
            actorId);

        CommandEnvelope envelope = command.ToCommandEnvelope();

        try
        {
            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                nameof(AggregateActor));

            return await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Actor invocation failed: CorrelationId={CorrelationId}, ActorId={ActorId}",
                command.CorrelationId,
                actorId);
            throw;
        }
    }
}
