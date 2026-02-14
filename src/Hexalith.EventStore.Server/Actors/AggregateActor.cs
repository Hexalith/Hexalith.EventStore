namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-4 (idempotency, tenant validation, state rehydration, domain invocation) are real.
/// Step 5: Event persistence (Story 3.7). Step 5b: Snapshot creation (Story 3.9).
/// State machine checkpointing deferred to Story 3.11.
/// SECURITY: Never use DaprClient.QueryStateAsync or bulk state queries without explicit tenant
/// filtering. DAPR query API does not enforce actor state scoping. See FR28.
/// SECURITY: Never bypass IActorStateManager with direct DaprClient.GetStateAsync/SetStateAsync.
/// Rule #6 exists to prevent this -- direct state store access bypasses actor state namespacing.
/// </summary>
public class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker,
    ISnapshotManager snapshotManager)
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
        // Story 3.10 will modify this to load snapshot first, then replay tail events only.
        // For now, lastSnapshotSequence tracks the sequence of the last snapshot created
        // during this actor activation (loaded from existing snapshot if present).
        var eventStreamReader = new EventStreamReader(
            StateManager,
            Host.LoggerFactory.CreateLogger<EventStreamReader>());

        object? currentState = await eventStreamReader
            .RehydrateAsync(command.AggregateIdentity)
            .ConfigureAwait(false);

        // Load existing snapshot to determine lastSnapshotSequence (H2 fix).
        // This avoids re-snapshotting on every call after the interval is first reached.
        SnapshotRecord? existingSnapshot = await snapshotManager
            .LoadSnapshotAsync(command.AggregateIdentity, StateManager, command.CorrelationId)
            .ConfigureAwait(false);

        long lastSnapshotSequence = existingSnapshot?.SequenceNumber ?? 0;

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

        // Extract domain service version from command extensions
        // (needed for both rejection and success event persistence)
        string domainServiceVersion = DaprDomainServiceInvoker.ExtractVersion(command, logger);

        // Handle domain rejection -- D3: rejection events are persisted like regular events
        if (domainResult.IsRejection)
        {
            var eventPersister = new EventPersister(
                StateManager,
                Host.LoggerFactory.CreateLogger<EventPersister>());

            long rejectionSequence = await eventPersister
                .PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion)
                .ConfigureAwait(false);

            // M2 fix: Rejection events count toward snapshot interval (D3).
            if (rejectionSequence > 0 && currentState is not null)
            {
                bool shouldSnapshot = await snapshotManager
                    .ShouldCreateSnapshotAsync(command.Domain, rejectionSequence, lastSnapshotSequence)
                    .ConfigureAwait(false);

                if (shouldSnapshot)
                {
                    long preEventSequence = rejectionSequence - domainResult.Events.Count;
                    await snapshotManager
                        .CreateSnapshotAsync(command.AggregateIdentity, preEventSequence, currentState, StateManager, command.CorrelationId)
                        .ConfigureAwait(false);
                }
            }

            var rejectionResult = new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: $"Domain rejection: {domainResult.Events[0].GetType().Name}",
                CorrelationId: command.CorrelationId,
                EventCount: domainResult.Events.Count);

            await idempotencyChecker.RecordAsync(causationId, rejectionResult).ConfigureAwait(false);

            try
            {
                await StateManager.SaveStateAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                throw new ConcurrencyConflictException(
                    command.CorrelationId,
                    command.AggregateId,
                    command.TenantId,
                    conflictSource: "StateStore",
                    innerException: ex);
            }

            return rejectionResult;
        }

        // Step 5: Event persistence (Story 3.7)
        long newSequence = 0;
        if (!domainResult.IsNoOp)
        {
            var eventPersister = new EventPersister(
                StateManager,
                Host.LoggerFactory.CreateLogger<EventPersister>());

            newSequence = await eventPersister
                .PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion)
                .ConfigureAwait(false);

            // Step 5b: Snapshot creation (Story 3.9)
            // Check if snapshot is due after event persistence. Snapshot is staged in the same
            // actor state batch and committed atomically with events by SaveStateAsync (AC #10).
            // Snapshot creation is advisory -- failure does not block command processing (rule #12).
            // H3 fix: currentState is the PRE-event state (before domain invocation). The snapshot
            // must record preEventSequence so that replay after snapshot load doesn't skip events.
            if (newSequence > 0 && currentState is not null)
            {
                bool shouldSnapshot = await snapshotManager
                    .ShouldCreateSnapshotAsync(command.Domain, newSequence, lastSnapshotSequence)
                    .ConfigureAwait(false);

                if (shouldSnapshot)
                {
                    long preEventSequence = newSequence - domainResult.Events.Count;
                    await snapshotManager
                        .CreateSnapshotAsync(command.AggregateIdentity, preEventSequence, currentState, StateManager, command.CorrelationId)
                        .ConfigureAwait(false);
                }
            }
        }

        // Create result and store for idempotency
        var result = new CommandProcessingResult(
            Accepted: true,
            CorrelationId: command.CorrelationId,
            EventCount: domainResult.Events.Count);

        await idempotencyChecker
            .RecordAsync(causationId, result)
            .ConfigureAwait(false);

        // Atomic commit of all state changes (D1: idempotency record + events + metadata + snapshot)
        try
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConcurrencyConflictException(
                command.CorrelationId,
                command.AggregateId,
                command.TenantId,
                conflictSource: "StateStore",
                innerException: ex);
        }

        return result;
    }
}
