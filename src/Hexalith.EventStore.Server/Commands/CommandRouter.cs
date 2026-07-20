
using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Commands;
/// <summary>
/// Routes commands to the correct aggregate actor based on canonical identity derivation.
/// SECURITY: Always derive actor ID from AggregateIdentity.ActorId. Never construct actor IDs
/// via manual string concatenation. The chain of custody from CommandEnvelope through
/// AggregateIdentity to actor ID must be unbroken. See FR15, FR28.
/// </summary>
public partial class CommandRouter(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions,
    ILogger<CommandRouter> logger) : ICommandRouter {
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandRouter"/> class with the default actor type name.
    /// </summary>
    /// <param name="actorProxyFactory">The DAPR actor proxy factory.</param>
    /// <param name="logger">The logger.</param>
    public CommandRouter(IActorProxyFactory actorProxyFactory, ILogger<CommandRouter> logger)
        : this(actorProxyFactory, Options.Create(new EventStoreActorOptions()), logger) {
    }

    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;
        string causationId = command.MessageId;

        Log.CommandRouting(logger, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);

        var envelope = command.ToCommandEnvelope();

        try {
            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                actorOptions.Value.AggregateActorTypeName);

            return await proxy.ProcessCommandAsync(envelope).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorInvocationFailed(logger, ex, command.CorrelationId, causationId, command.Tenant, command.Domain, command.AggregateId, command.CommandType, actorId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<CommandProcessingResult> RouteFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();
        if (executionContext.FencingToken <= 0
            || !string.Equals(executionContext.MessageId, command.MessageId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CorrelationId, command.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(executionContext.Tenant, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(executionContext.Domain, command.Domain, StringComparison.Ordinal)
            || !string.Equals(executionContext.AggregateId, command.AggregateId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CommandType, command.CommandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        string actorId = identity.ActorId;
        Log.CommandRouting(
            logger,
            command.CorrelationId,
            command.MessageId,
            command.Tenant,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            actorId);
        var request = new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext);
        try
        {
            IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(actorId),
                actorOptions.Value.AggregateActorTypeName);
            return await proxy.ProcessFencedCommandAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.ActorInvocationFailed(
                logger,
                ex,
                command.CorrelationId,
                command.MessageId,
                command.Tenant,
                command.Domain,
                command.AggregateId,
                command.CommandType,
                actorId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();
        if (executionContext.FencingToken <= 0
            || !string.Equals(executionContext.MessageId, command.MessageId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CorrelationId, command.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(executionContext.Tenant, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(executionContext.Domain, command.Domain, StringComparison.Ordinal)
            || !string.Equals(executionContext.AggregateId, command.AggregateId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CommandType, command.CommandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }

        var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
        IAggregateActor proxy = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            actorOptions.Value.AggregateActorTypeName);
        return await proxy.ReconcileFencedCommandAsync(
            new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext)).ConfigureAwait(false);
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
