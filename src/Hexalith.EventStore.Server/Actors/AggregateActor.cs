namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-2 (idempotency, tenant validation) are real. Steps 3-5 are stubs for Stories 3.4-3.11.
/// </summary>
public class AggregateActor(ActorHost host, ILogger<AggregateActor> logger)
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

        // Step 3: State rehydration (STUB -- Story 3.4)
        logger.LogDebug("Step 3: State rehydration -- STUB (Story 3.4)");

        // Step 4: Domain service invocation (STUB -- Story 3.5)
        logger.LogDebug("Step 4: Domain service invocation -- STUB (Story 3.5)");

        // Step 5: State machine execution (STUB -- Story 3.11)
        logger.LogDebug("Step 5: State machine execution -- STUB (Story 3.11)");

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
