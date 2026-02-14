namespace Hexalith.EventStore.Server.Actors;

using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-4 (idempotency, tenant validation, state rehydration, domain invocation) are real.
/// Step 5: Event persistence (Story 3.7). Step 5b: Snapshot creation (Story 3.9).
/// Story 3.10: Step 3 now loads snapshot FIRST, passes to EventStreamReader for tail-only reads.
/// Story 3.11: Checkpointed state machine, OpenTelemetry activities, advisory status writes.
/// SECURITY: Never use DaprClient.QueryStateAsync or bulk state queries without explicit tenant
/// filtering. DAPR query API does not enforce actor state scoping. See FR28.
/// SECURITY: Never bypass IActorStateManager with direct DaprClient.GetStateAsync/SetStateAsync.
/// Rule #6 exists to prevent this -- direct state store access bypasses actor state namespacing.
/// </summary>
public class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker,
    ISnapshotManager snapshotManager,
    ICommandStatusStore commandStatusStore)
    : Actor(host), IAggregateActor
{
    /// <inheritdoc/>
    public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using Activity? processActivity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.ProcessCommand);
        SetActivityTags(processActivity, command);

        long startTicks = Stopwatch.GetTimestamp();

        logger.LogInformation(
            "Actor {ActorId} received command: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        // Per-call helpers (require actor's IActorStateManager)
        var idempotencyChecker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>());
        var stateMachine = new ActorStateMachine(
            StateManager,
            Host.LoggerFactory.CreateLogger<ActorStateMachine>());
        string pipelineKeyPrefix = command.AggregateIdentity.PipelineKeyPrefix;
        string causationId = command.CausationId ?? command.CorrelationId;

        // Step 1: Idempotency check (keyed by CausationId -- see design decision F8)
        using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.IdempotencyCheck))
        {
            SetActivityTags(activity, command);

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
                processActivity?.SetStatus(ActivityStatusCode.Ok);
                return cached;
            }

            // Step 1b: Check for in-flight pipeline state (resume detection -- AC #8)
            PipelineState? existingPipeline = await stateMachine
                .LoadPipelineStateAsync(pipelineKeyPrefix, command.CorrelationId)
                .ConfigureAwait(false);

            if (existingPipeline is not null)
            {
                logger.LogWarning(
                    "Resume detected: Actor {ActorId} resuming from stage {Stage}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
                    Host.Id,
                    existingPipeline.CurrentStage,
                    command.CorrelationId,
                    command.TenantId,
                    command.Domain,
                    command.AggregateId,
                    command.CommandType);

                if (existingPipeline.CurrentStage == CommandStatus.EventsStored)
                {
                    // Events already persisted (AC #2). Skip re-persistence, proceed to terminal.
                    return await ResumeFromEventsStoredAsync(
                        command, causationId, existingPipeline, idempotencyChecker, stateMachine,
                        pipelineKeyPrefix, processActivity).ConfigureAwait(false);
                }

                // Processing or unexpected stage: crash happened before events were persisted.
                // Safe to reprocess from scratch -- clean up stale state and continue normal flow.
                await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                    .ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);
                // Fall through to normal processing below
            }
        }

        // SEC-2 CRITICAL: This MUST execute before any state access (Step 3+) (F-PM2)
        // Step 2: Tenant validation (SEC-2 -- BEFORE state access)
        using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.TenantValidation))
        {
            SetActivityTags(activity, command);

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
                await StateManager.SaveStateAsync().ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch");
                processActivity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch");
                return rejectionResult;
            }
        }

        // Checkpoint Processing stage (AC #1, #7)
        var pipelineState = new PipelineState(
            command.CorrelationId,
            CommandStatus.Processing,
            command.CommandType,
            DateTimeOffset.UtcNow,
            EventCount: null,
            RejectionEventType: null);
        await stateMachine.CheckpointAsync(pipelineKeyPrefix, pipelineState).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        LogStageTransition(CommandStatus.Processing, command, startTicks);
        await WriteAdvisoryStatusAsync(command, CommandStatus.Processing).ConfigureAwait(false);

        // Step 3: State rehydration (Story 3.10 -- snapshot-first flow)
        SnapshotRecord? existingSnapshot;
        RehydrationResult? rehydrationResult;
        long lastSnapshotSequence;
        object? currentState;

        using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.StateRehydration))
        {
            SetActivityTags(activity, command);

            existingSnapshot = await snapshotManager
                .LoadSnapshotAsync(command.AggregateIdentity, StateManager, command.CorrelationId)
                .ConfigureAwait(false);

            var eventStreamReader = new EventStreamReader(
                StateManager,
                Host.LoggerFactory.CreateLogger<EventStreamReader>());

            rehydrationResult = await eventStreamReader
                .RehydrateAsync(command.AggregateIdentity, existingSnapshot)
                .ConfigureAwait(false);

            lastSnapshotSequence = rehydrationResult?.LastSnapshotSequence ?? 0;
            currentState = ConstructDomainState(rehydrationResult);

            logger.LogInformation(
                "State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
                currentState?.GetType().Name ?? "null",
                Host.Id,
                command.CorrelationId);
        }

        // Step 4: Domain service invocation (Story 3.5)
        DomainResult domainResult;
        using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.DomainServiceInvoke))
        {
            SetActivityTags(activity, command);

            domainResult = await domainServiceInvoker
                .InvokeAsync(command, currentState)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Domain service result: {ResultType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
                domainResult.IsSuccess ? "Success" : domainResult.IsRejection ? "Rejection" : "NoOp",
                Host.Id,
                command.CorrelationId);
        }

        string domainServiceVersion = DaprDomainServiceInvoker.ExtractVersion(command, logger);

        // Handle no-op path (AC #12): Processing -> Completed directly
        if (domainResult.IsNoOp)
        {
            return await CompleteTerminalAsync(
                command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                accepted: true, eventCount: 0, errorMessage: null,
                processActivity, startTicks).ConfigureAwait(false);
        }

        // Step 5: Event persistence (Story 3.7)
        using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsPersist))
        {
            SetActivityTags(activity, command);

            var eventPersister = new EventPersister(
                StateManager,
                Host.LoggerFactory.CreateLogger<EventPersister>());

            long newSequence = await eventPersister
                .PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion)
                .ConfigureAwait(false);

            // Step 5b: Snapshot creation (Story 3.9)
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

            // Checkpoint EventsStored in SAME batch as events (AC #9)
            string? rejectionEventType = domainResult.IsRejection
                ? domainResult.Events[0].GetType().Name
                : null;
            var eventsStoredState = new PipelineState(
                command.CorrelationId,
                CommandStatus.EventsStored,
                command.CommandType,
                pipelineState.StartedAt,
                EventCount: domainResult.Events.Count,
                RejectionEventType: rejectionEventType);
            await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsStoredState).ConfigureAwait(false);

            // Atomic commit: events + snapshot + EventsStored checkpoint (AC #9)
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

            LogStageTransition(CommandStatus.EventsStored, command, startTicks);
            await WriteAdvisoryStatusAsync(command, CommandStatus.EventsStored).ConfigureAwait(false);
        }

        // Epic 4 integration point: EventPublisher.PublishAsync() goes HERE.
        // When Story 4.1 implements the EventPublisher, insert the publish call between
        // the EventsStored checkpoint commit above and the terminal completion below.
        // If publish succeeds: checkpoint EventsPublished, then proceed to Completed.
        // If publish permanently fails: checkpoint PublishFailed (terminal).

        // Terminal state
        bool accepted = !domainResult.IsRejection;
        string? errorMessage = domainResult.IsRejection
            ? $"Domain rejection: {domainResult.Events[0].GetType().Name}"
            : null;

        return await CompleteTerminalAsync(
            command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
            accepted, domainResult.Events.Count, errorMessage,
            processActivity, startTicks).ConfigureAwait(false);
    }

    /// <summary>
    /// Constructs the currentState for domain service invocation from the RehydrationResult (AC #11).
    /// Three cases:
    /// - SnapshotState non-null + Events non-empty: pass RehydrationResult (domain applies tail events to snapshot)
    /// - SnapshotState non-null + Events empty: pass SnapshotState directly (snapshot IS current state)
    /// - SnapshotState null: pass Events list (full replay, backward compatible)
    /// </summary>
    private static object? ConstructDomainState(RehydrationResult? rehydrationResult)
    {
        if (rehydrationResult is null)
        {
            return null; // New aggregate
        }

        if (rehydrationResult.SnapshotState is not null && rehydrationResult.Events.Count > 0)
        {
            // Snapshot + tail events: domain service must apply tail events to snapshot state
            return rehydrationResult;
        }

        if (rehydrationResult.SnapshotState is not null)
        {
            // Snapshot at current sequence: snapshot IS the current state
            return rehydrationResult.SnapshotState;
        }

        // No snapshot: full event list (backward compatible with pre-3.10 behavior)
        return rehydrationResult.Events;
    }

    private static void SetActivityTags(Activity? activity, CommandEnvelope command)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(EventStoreActivitySource.TagCorrelationId, command.CorrelationId);
        activity.SetTag(EventStoreActivitySource.TagTenantId, command.TenantId);
        activity.SetTag(EventStoreActivitySource.TagDomain, command.Domain);
        activity.SetTag(EventStoreActivitySource.TagAggregateId, command.AggregateId);
        activity.SetTag(EventStoreActivitySource.TagCommandType, command.CommandType);
    }

    /// <summary>
    /// Resumes from EventsStored stage after crash recovery (AC #2, #8).
    /// Events are already persisted -- skip re-persistence, proceed directly to terminal.
    /// </summary>
    private async Task<CommandProcessingResult> ResumeFromEventsStoredAsync(
        CommandEnvelope command,
        string causationId,
        PipelineState existingPipeline,
        IdempotencyChecker idempotencyChecker,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        Activity? processActivity)
    {
        bool accepted = existingPipeline.RejectionEventType is null;
        string? errorMessage = existingPipeline.RejectionEventType is not null
            ? $"Domain rejection: {existingPipeline.RejectionEventType}"
            : null;

        var result = new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: command.CorrelationId,
            EventCount: existingPipeline.EventCount ?? 0);

        await idempotencyChecker.RecordAsync(causationId, result).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

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

        CommandStatus terminalStatus = accepted ? CommandStatus.Completed : CommandStatus.Rejected;
        await WriteAdvisoryStatusAsync(command, terminalStatus).ConfigureAwait(false);

        logger.LogInformation(
            "Resume completed: Actor {ActorId} stage transition: Stage={Stage}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            terminalStatus,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        processActivity?.SetStatus(ActivityStatusCode.Ok);
        return result;
    }

    /// <summary>
    /// Completes terminal state: records idempotency, cleans up pipeline, commits, writes advisory status.
    /// </summary>
    private async Task<CommandProcessingResult> CompleteTerminalAsync(
        CommandEnvelope command,
        string causationId,
        IdempotencyChecker idempotencyChecker,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        bool accepted,
        int eventCount,
        string? errorMessage,
        Activity? processActivity,
        long startTicks)
    {
        var result = new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: command.CorrelationId,
            EventCount: eventCount);

        await idempotencyChecker.RecordAsync(causationId, result).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

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

        CommandStatus terminalStatus = accepted ? CommandStatus.Completed : CommandStatus.Rejected;
        LogStageTransition(terminalStatus, command, startTicks);
        await WriteAdvisoryStatusAsync(command, terminalStatus).ConfigureAwait(false);

        processActivity?.SetStatus(ActivityStatusCode.Ok);
        return result;
    }

    /// <summary>
    /// Writes advisory command status. Failures are logged at Warning level and never thrown (rule #12).
    /// </summary>
    private async Task WriteAdvisoryStatusAsync(CommandEnvelope command, CommandStatus status)
    {
        try
        {
            await commandStatusStore.WriteStatusAsync(
                command.TenantId,
                command.CorrelationId,
                new CommandStatusRecord(
                    status,
                    DateTimeOffset.UtcNow,
                    command.AggregateId,
                    EventCount: null,
                    RejectionEventType: null,
                    FailureReason: null,
                    TimeoutDuration: null)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Rule #12: Advisory status writes -- failure logged, never thrown.
            logger.LogWarning(
                ex,
                "Advisory status write failed: CorrelationId={CorrelationId}, Status={Status}",
                command.CorrelationId,
                status);
        }
    }

    /// <summary>
    /// Logs a structured stage transition with all required fields (AC #6).
    /// Rule #5: Never logs event payload data -- only envelope metadata fields.
    /// Rule #9: CorrelationId in every structured log entry.
    /// </summary>
    private void LogStageTransition(CommandStatus stage, CommandEnvelope command, long startTicks)
    {
        double durationMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
        logger.LogInformation(
            "Actor {ActorId} stage transition: Stage={Stage}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, DurationMs={DurationMs}",
            Host.Id,
            stage,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            durationMs);
    }
}
