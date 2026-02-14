namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-3 (idempotency, tenant validation, state rehydration) are real.
/// Step 4: Domain service invocation (Story 3.5). Step 5: State machine stub (Story 3.11).
/// </summary>
public class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker)
    : Actor(host), IAggregateActor
{
    /// <inheritdoc/>
    public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.LogInformation(
            "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        // Step 1: Idempotency check (keyed by CausationId -- see design decision F8)
        string causationId = command.CausationId ?? command.CorrelationId;
        var idempotencyChecker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>());

        CommandProcessingResult? cached = await idempotencyChecker
            .CheckAsync(causationId)
            .ConfigureAwait(false);

        if (cached is not null)
        {
            logger.LogInformation(
                "Duplicate command detected: CausationId={CausationId}, CorrelationId={CorrelationId}, ActorId={ActorId}. Returning cached result.",
                causationId,
                command.CorrelationId,
                Host.Id);
            return cached;
        }

        // SEC-2 CRITICAL: This MUST execute before any state access (Step 3+) (F-PM2)
        // Step 2: Tenant validation (SEC-2 -- BEFORE state access)
        var tenantValidator = new TenantValidator(
            Host.LoggerFactory.CreateLogger<TenantValidator>());
        try
        {
            tenantValidator.Validate(command.TenantId, Host.Id.GetId());
        }
        catch (TenantMismatchException ex) // F-PM4: catch specifically BEFORE any broader catch blocks
        {
            logger.LogWarning(
                "Tenant validation rejected command: CorrelationId={CorrelationId}, CommandTenant={CommandTenant}, ActorTenant={ActorTenant}",
                command.CorrelationId,
                ex.CommandTenant,
                ex.ActorTenant);

            var rejectionResult = new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: ex.Message,
                CorrelationId: command.CorrelationId);

            await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);
            // F-PM7: This SaveStateAsync commits ONLY the idempotency rejection record.
            // If future steps are added between Step 1 and Step 2, review whether their
            // buffered state changes should be committed on the rejection path.
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            return rejectionResult;
        }

        // Step 3: State rehydration (Story 3.4)
        var eventStreamReader = new EventStreamReader(
            StateManager,
            Host.LoggerFactory.CreateLogger<EventStreamReader>());

        object? currentState = await eventStreamReader
            .RehydrateAsync(command.AggregateIdentity)
            .ConfigureAwait(false);

        logger.LogInformation(
            "State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
            currentState?.GetType().Name ?? "null",
            Host.Id,
            command.CorrelationId);

        // Step 4: Domain service invocation (Story 3.5)
        DomainResult domainResult = await domainServiceInvoker
            .InvokeAsync(command, currentState)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Domain service result: {ResultType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
            domainResult.IsSuccess ? "Success" : domainResult.IsRejection ? "Rejection" : "NoOp",
            Host.Id,
            command.CorrelationId);

        // Handle domain rejection
        if (domainResult.IsRejection)
        {
            var rejectionResult = new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: $"Domain rejection: {domainResult.Events[0].GetType().Name}",
                CorrelationId: command.CorrelationId);

            await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            return rejectionResult;
        }

        // Handle no-op (empty event list -- command acknowledged, no state change)
        if (domainResult.IsNoOp)
        {
            var noOpResult = new CommandProcessingResult(
                Accepted: true,
                CorrelationId: command.CorrelationId);

            await idempotencyChecker.RecordAsync(causationId, noOpResult).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            return noOpResult;
        }

        // Success: events produced -- pass to Step 5
        // Step 5: State machine execution (STUB -- Story 3.11)
        logger.LogDebug(
            "Step 5: State machine execution -- STUB (Story 3.11), {EventCount} events to persist",
            domainResult.Events.Count);

        // Create result and store for idempotency
        var result = new CommandProcessingResult(
            Accepted: true,
            CorrelationId: command.CorrelationId);

        await idempotencyChecker
            .RecordAsync(causationId, result)
            .ConfigureAwait(false);

        // Atomic commit of all state changes
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        return result;
    }
}
