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
public partial class CommandRouter(
    IActorProxyFactory actorProxyFactory,
    ILogger<CommandRouter> logger) : ICommandRouter {
    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;
        // At this layer, CausationId = CorrelationId (original submission, not replay)
        string causationId = command.CorrelationId;

        Log.CommandRouting(logger, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);

        CommandEnvelope envelope = command.ToCommandEnvelope();

        try {
            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                nameof(AggregateActor));

            return await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorInvocationFailed(logger, ex, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);
            throw;
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1100,
            Level = LogLevel.Debug,
            Message = "Routing command to actor: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, ActorId={ActorId}, Stage=CommandRouting")]
        public static partial void CommandRouting(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string actorId);

        [LoggerMessage(
            EventId = 1101,
            Level = LogLevel.Error,
            Message = "Actor invocation failed: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, ActorId={ActorId}, Stage=ActorInvocationFailed")]
        public static partial void ActorInvocationFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string actorId);
    }
}
