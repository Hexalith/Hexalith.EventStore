using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Holds one provider/model turn through authoritative validation and target aggregate commit.</summary>
public sealed class CoordinatedCommandActor(
    ActorHost host,
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreActorOptions> actorOptions,
    IEnumerable<ICoordinatedCommandPolicy> policies,
    IEventPayloadProtectionService payloadProtectionService) : Actor(host), ICoordinatedCommandActor
{
    /// <summary>Gets the registered Dapr actor type name.</summary>
    public const string ActorTypeName = nameof(CoordinatedCommandActor);

    /// <inheritdoc />
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
        => ProcessAsync(command, executionContext: null);

    /// <inheritdoc />
    public Task<CommandProcessingResult> ProcessFencedCommandAsync(FencedCommandEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProcessAsync(request.Command, request.ExecutionContext);
    }

    private async Task<CommandProcessingResult> ProcessAsync(
        CommandEnvelope command,
        IdempotencyExecutionContext? executionContext)
    {
        ArgumentNullException.ThrowIfNull(command);
        ICoordinatedCommandPolicy[] claiming = [.. policies.Where(policy => policy.Claims(command.Domain, command.CommandType)).Take(2)];
        if (claiming.Length != 1)
        {
            throw new InvalidOperationException("Exactly one coordinated command policy is required.");
        }

        ICoordinatedCommandPolicy policy = claiming[0];
        CoordinatedCommandScope scope = policy.GetScope(command);
        if (!string.Equals(scope.Source.ActorId, Host.Id.GetId(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The command does not belong to this coordination actor.");
        }

        IAggregateActor source = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(scope.Source.ActorId), actorOptions.Value.AggregateActorTypeName);
        AggregateIdentity targetIdentity = new(command.TenantId, command.Domain, command.AggregateId);
        IAggregateActor target = targetIdentity.ActorId == scope.Source.ActorId
            ? source
            : actorProxyFactory.CreateActorProxy<IAggregateActor>(
                new ActorId(targetIdentity.ActorId), actorOptions.Value.AggregateActorTypeName);

        if (executionContext is not null)
        {
            IdempotencyCheckResult priorTarget = await target.ReconcileFencedCommandAsync(
                new FencedCommandEnvelope(command, executionContext)).ConfigureAwait(false);
            if (priorTarget.Outcome is IdempotencyCheckOutcome.ExactTerminalDuplicate
                or IdempotencyCheckOutcome.RetryableRecoverable)
            {
                return priorTarget.Result ?? throw new InvalidOperationException("A terminal target outcome has no result.");
            }

            if (priorTarget.Outcome != IdempotencyCheckOutcome.Miss)
            {
                throw new InvalidOperationException("The target command identity cannot be safely reconciled.");
            }
        }

        byte[] rejectionIdentity = JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.MessageId,
        });
        string rejectionKey = $"rejection:{Convert.ToHexString(SHA256.HashData(rejectionIdentity))}";
        string digest = policy.GetCommandDigest(command);
        ConditionalValue<CoordinatedCommandRejection> prior = await StateManager
            .TryGetStateAsync<CoordinatedCommandRejection>(rejectionKey).ConfigureAwait(false);
        if (prior.HasValue)
        {
            return string.Equals(prior.Value.CommandDigest, digest, StringComparison.Ordinal)
                ? prior.Value.Result
                : new CommandProcessingResult(false, "command_identity_conflict", command.CorrelationId);
        }

        if (scope.RequiresSourceValidation)
        {
            EventEnvelope[] events = await source.GetEventsAsync(0).ConfigureAwait(false);
            ProjectionEventReadabilityResult readable = await ProjectionEventWireBuilder.BuildAsync(
                payloadProtectionService, scope.Source, events, CancellationToken.None).ConfigureAwait(false);
            if (readable.Events is null)
            {
                throw new InvalidOperationException("Authoritative source events are unavailable for coordination.");
            }

            if (!policy.Validate(command, readable.Events))
            {
                // A non-fenced retry can reach this path after its target commit and a later source update.
                // Let the target actor return its authoritative idempotency outcome when its event exists.
                // Persisted events get fresh message IDs; the submitted command message ID is their causation.
                if (executionContext is null && (await target.GetEventsAsync(0).ConfigureAwait(false))
                    .Any(item => string.Equals(item.CausationId, command.MessageId, StringComparison.Ordinal)))
                {
                    return await target.ProcessCommandAsync(command).ConfigureAwait(false);
                }

                var rejected = new CommandProcessingResult(false, "coordinated_source_conflict", command.CorrelationId,
                    FailureReason: "ConcurrencyConflict");
                await StateManager.SetStateAsync(rejectionKey,
                    new CoordinatedCommandRejection(digest, rejected)).ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);
                return rejected;
            }
        }

        return executionContext is null
            ? await target.ProcessCommandAsync(command).ConfigureAwait(false)
            : await target.ProcessFencedCommandAsync(new FencedCommandEnvelope(command, executionContext)).ConfigureAwait(false);
    }
}
