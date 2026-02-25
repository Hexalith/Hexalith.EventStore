
using System.Diagnostics;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-4 (idempotency, tenant validation, state rehydration, domain invocation) are real.
/// Step 5: Event persistence (Story 3.7). Step 5b: Snapshot creation (Story 3.9).
/// Story 3.10: Step 3 now loads snapshot FIRST, passes to EventStreamReader for tail-only reads.
/// Story 3.11: Checkpointed state machine, OpenTelemetry activities, advisory status writes.
/// Story 4.4: IRemindable for drain recovery of unpublished events after pub/sub outage.
/// Story 4.5: Dead-letter routing for infrastructure failures at Steps 3-5.
/// SECURITY: Never use DaprClient.QueryStateAsync or bulk state queries without explicit tenant
/// filtering. DAPR query API does not enforce actor state scoping. See FR28.
/// SECURITY: Never bypass IActorStateManager with direct DaprClient.GetStateAsync/SetStateAsync.
/// Rule #6 exists to prevent this -- direct state store access bypasses actor state namespacing.
/// </summary>
public partial class AggregateActor(
    ActorHost host,
    ILogger<AggregateActor> logger,
    IDomainServiceInvoker domainServiceInvoker,
    ISnapshotManager snapshotManager,
    ICommandStatusStore commandStatusStore,
    IEventPublisher eventPublisher,
    IOptions<EventDrainOptions> drainOptions,
    IDeadLetterPublisher deadLetterPublisher)
    : Actor(host), IAggregateActor, IRemindable {
    private const string TraceParentExtensionKey = "traceparent";
    private const string TraceStateExtensionKey = "tracestate";

    private const int MaxConcurrentStateReads = 32;
    /// <inheritdoc/>
    public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command) {
        ArgumentNullException.ThrowIfNull(command);

        Activity? processActivity;
        if (Activity.Current is null && TryGetFallbackParentContext(command, out ActivityContext fallbackParent)) {
            processActivity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.ProcessCommand,
                ActivityKind.Internal,
                fallbackParent);
        }
        else {
            processActivity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.ProcessCommand,
                ActivityKind.Internal);
        }

        using (processActivity) {
            SetActivityTags(processActivity, command);

            long startTicks = Stopwatch.GetTimestamp();

            string causationId = command.CausationId ?? command.CorrelationId;

            Log.ActorActivated(logger, Host.Id.GetId(), command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType);

            // Per-call helpers (require actor's IActorStateManager)
            var idempotencyChecker = new IdempotencyChecker(
                StateManager,
                Host.LoggerFactory.CreateLogger<IdempotencyChecker>());
            var stateMachine = new ActorStateMachine(
                StateManager,
                Host.LoggerFactory.CreateLogger<ActorStateMachine>());
            string pipelineKeyPrefix = command.AggregateIdentity.PipelineKeyPrefix;

            // Step 1: Idempotency check (keyed by CausationId -- see design decision F8)
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.IdempotencyCheck,
                ActivityKind.Internal)) {
                SetActivityTags(activity, command);

                CommandProcessingResult? cached = await idempotencyChecker
                    .CheckAsync(causationId)
                    .ConfigureAwait(false);

                if (cached is not null) {
                    logger.LogInformation(
                        "Duplicate command detected: CausationId={CausationId}, CorrelationId={CorrelationId}, ActorId={ActorId}. Returning cached result.",
                        causationId,
                        command.CorrelationId,
                        Host.Id);
                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Ok));
                    return cached;
                }

                // Step 1b: Check for in-flight pipeline state (resume detection -- AC #8)
                PipelineState? existingPipeline = await stateMachine
                    .LoadPipelineStateAsync(pipelineKeyPrefix, command.CorrelationId)
                    .ConfigureAwait(false);

                if (existingPipeline is not null) {
                    logger.LogWarning(
                        "Resume detected: Actor {ActorId} resuming from stage {Stage}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
                        Host.Id,
                        existingPipeline.CurrentStage,
                        command.CorrelationId,
                        command.TenantId,
                        command.Domain,
                        command.AggregateId,
                        command.CommandType);

                    if (existingPipeline.CurrentStage == CommandStatus.EventsStored) {
                        // Events already persisted (AC #2). Skip re-persistence, proceed to terminal.
                        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                        return await ResumeFromEventsStoredAsync(
                            command, causationId, existingPipeline, idempotencyChecker, stateMachine,
                            pipelineKeyPrefix, processActivity, startTicks).ConfigureAwait(false);
                    }

                    // Processing or unexpected stage: crash happened before events were persisted.
                    // Safe to reprocess from scratch -- clean up stale state and continue normal flow.
                    await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                        .ConfigureAwait(false);
                    await StateManager.SaveStateAsync().ConfigureAwait(false);
                    // Fall through to normal processing below
                }

                _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            }

            // SEC-2 CRITICAL: This MUST execute before any state access (Step 3+) (F-PM2)
            // Step 2: Tenant validation (SEC-2 -- BEFORE state access)
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.TenantValidation,
                ActivityKind.Internal)) {
                SetActivityTags(activity, command);

                var tenantValidator = new TenantValidator(
                    Host.LoggerFactory.CreateLogger<TenantValidator>());
                try {
                    tenantValidator.Validate(command.TenantId, Host.Id.GetId());
                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }
                catch (TenantMismatchException ex) // F-PM4: catch specifically BEFORE any broader catch blocks
                {
                    logger.LogWarning(
                        "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, CommandTenant={CommandTenant}, ActorTenant={ActorTenant}",
                        "TenantMismatch",
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
                    _ = (activity?.AddException(ex));
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch"));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch"));
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

            LogStageTransition(CommandStatus.Processing, command, causationId, startTicks);
            await WriteAdvisoryStatusAsync(command, CommandStatus.Processing).ConfigureAwait(false);

            // Step 3: State rehydration (Story 3.10 -- snapshot-first flow)
            // Story 4.5: Infrastructure exceptions trigger dead-letter routing.
            SnapshotRecord? existingSnapshot;
            RehydrationResult? rehydrationResult;
            long lastSnapshotSequence;
            object? currentState;

            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.StateRehydration,
                ActivityKind.Internal)) {
                SetActivityTags(activity, command);

                try {
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

                    // Maintain domain processor contract compatibility: always pass List<EventEnvelope> (or null)
                    // to the domain service. Snapshot-assisted rehydration is still used for lastSnapshotSequence
                    // tracking and optimized actor-level state loading, but domain invocation remains backward-compatible.
                    if (rehydrationResult?.UsedSnapshot == true) {
                        logger.LogDebug(
                            "Snapshot-assisted rehydration detected for ActorId={ActorId}, CorrelationId={CorrelationId}. Using full replay event list for domain compatibility.",
                            Host.Id,
                            command.CorrelationId);

                        RehydrationResult? fullReplayResult = await eventStreamReader
                            .RehydrateAsync(command.AggregateIdentity)
                            .ConfigureAwait(false);

                        currentState = fullReplayResult?.Events;
                    }
                    else {
                        currentState = rehydrationResult?.Events;
                    }

                    logger.LogInformation(
                        "State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
                        currentState?.GetType().Name ?? "null",
                        Host.Id,
                        command.CorrelationId);

                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    _ = (activity?.AddException(ex));
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
                    // Story 4.5: State rehydration infrastructure failure -- dead-letter routing
                    return await HandleInfrastructureFailureAsync(
                        command, causationId, CommandStatus.Processing, ex,
                        idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        processActivity, startTicks, eventCount: null).ConfigureAwait(false);
                }
            }

            // Step 4: Domain service invocation (Story 3.5)
            // Story 4.5: Infrastructure exceptions trigger dead-letter routing.
            // D3: Domain rejections (IRejectionEvent) are normal events, NOT dead-letter triggers.
            DomainResult domainResult;
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.DomainServiceInvoke,
                ActivityKind.Client)) {
                SetActivityTags(activity, command);

                try {
                    domainResult = await domainServiceInvoker
                        .InvokeAsync(command, currentState)
                        .ConfigureAwait(false);

                    logger.LogInformation(
                        "Domain service result: {ResultType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
                        domainResult.IsSuccess ? "Success" : domainResult.IsRejection ? "Rejection" : "NoOp",
                        Host.Id,
                        command.CorrelationId);

                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    _ = (activity?.AddException(ex));
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
                    // Story 4.5: Domain service invocation infrastructure failure -- dead-letter routing
                    return await HandleInfrastructureFailureAsync(
                        command, causationId, CommandStatus.Processing, ex,
                        idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        processActivity, startTicks, eventCount: null).ConfigureAwait(false);
                }
            }

            string domainServiceVersion = DaprDomainServiceInvoker.ExtractVersion(command, logger);

            // Handle no-op path (AC #12): Processing -> Completed directly
            if (domainResult.IsNoOp) {
                return await CompleteTerminalAsync(
                    command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                    accepted: true, eventCount: 0, errorMessage: null,
                    processActivity, startTicks).ConfigureAwait(false);
            }

            // Step 5: Event persistence (Story 3.7)
            // Story 4.5: Infrastructure exceptions trigger dead-letter routing.
            EventPersistResult persistResult;
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.EventsPersist,
                ActivityKind.Internal)) {
                SetActivityTags(activity, command);

                try {
                    var eventPersister = new EventPersister(
                        StateManager,
                        Host.LoggerFactory.CreateLogger<EventPersister>());

                    persistResult = await eventPersister
                        .PersistEventsAsync(command.AggregateIdentity, command, domainResult, domainServiceVersion)
                        .ConfigureAwait(false);

                    // Step 5b: Snapshot creation (Story 3.9)
                    if (persistResult.NewSequenceNumber > 0 && currentState is not null) {
                        bool shouldSnapshot = await snapshotManager
                            .ShouldCreateSnapshotAsync(command.Domain, persistResult.NewSequenceNumber, lastSnapshotSequence)
                            .ConfigureAwait(false);

                        if (shouldSnapshot) {
                            long preEventSequence = persistResult.NewSequenceNumber - domainResult.Events.Count;
                            await snapshotManager
                                .CreateSnapshotAsync(command.AggregateIdentity, preEventSequence, currentState, StateManager, command.CorrelationId)
                                .ConfigureAwait(false);
                        }
                    }

                    // Checkpoint EventsStored in SAME batch as events (AC #9)
                    string? rejectionEventType = domainResult.IsRejection
                        ? GetEventTypeName(domainResult.Events[0])
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
                    try {
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex) {
                        throw new ConcurrencyConflictException(
                            command.CorrelationId,
                            command.AggregateId,
                            command.TenantId,
                            conflictSource: "StateStore",
                            innerException: ex);
                    }

                    LogStageTransition(CommandStatus.EventsStored, command, causationId, startTicks);
                    await WriteAdvisoryStatusAsync(command, CommandStatus.EventsStored).ConfigureAwait(false);

                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyConflictException) {
                    _ = (activity?.AddException(ex));
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
                    // Story 4.5: Event persistence infrastructure failure -- dead-letter routing
                    return await HandleInfrastructureFailureAsync(
                        command, causationId, CommandStatus.EventsStored, ex,
                        idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        processActivity, startTicks, eventCount: domainResult.Events.Count).ConfigureAwait(false);
                }
            }

            // Story 4.1: Publish events via DAPR pub/sub with CloudEvents 1.0
            // Rejection events ARE published (D3: rejection events are normal events).
            EventPublishResult publishResult = await eventPublisher
                .PublishEventsAsync(command.AggregateIdentity, persistResult.PersistedEnvelopes, command.CorrelationId)
                .ConfigureAwait(false);

            if (publishResult.Success) {
                // Checkpoint EventsPublished
                var eventsPublishedState = new PipelineState(
                    command.CorrelationId,
                    CommandStatus.EventsPublished,
                    command.CommandType,
                    pipelineState.StartedAt,
                    EventCount: domainResult.Events.Count,
                    RejectionEventType: domainResult.IsRejection ? GetEventTypeName(domainResult.Events[0]) : null);
                await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsPublishedState).ConfigureAwait(false);

                LogStageTransition(CommandStatus.EventsPublished, command, causationId, startTicks);
                await WriteAdvisoryStatusAsync(command, CommandStatus.EventsPublished).ConfigureAwait(false);

                // Terminal state: Completed (or Rejected advisory)
                bool accepted = !domainResult.IsRejection;
                string? errorMessage = domainResult.IsRejection
                    ? $"Domain rejection: {GetEventTypeName(domainResult.Events[0])}"
                    : null;

                return await CompleteTerminalAsync(
                    command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                    accepted, domainResult.Events.Count, errorMessage,
                    processActivity, startTicks).ConfigureAwait(false);
            }
            else {
                // Publication failed: transition to PublishFailed terminal state
                var publishFailedState = new PipelineState(
                    command.CorrelationId,
                    CommandStatus.PublishFailed,
                    command.CommandType,
                    pipelineState.StartedAt,
                    EventCount: domainResult.Events.Count,
                    RejectionEventType: domainResult.IsRejection ? GetEventTypeName(domainResult.Events[0]) : null);
                await stateMachine.CheckpointAsync(pipelineKeyPrefix, publishFailedState).ConfigureAwait(false);

                // Cleanup pipeline and commit atomically
                await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                    .ConfigureAwait(false);

                var failResult = new CommandProcessingResult(
                    Accepted: false,
                    ErrorMessage: $"Event publication failed: {publishResult.FailureReason}",
                    CorrelationId: command.CorrelationId,
                    EventCount: domainResult.Events.Count);

                await idempotencyChecker.RecordAsync(causationId, failResult).ConfigureAwait(false);

                // Story 4.4: Store drain record for recovery (committed in same atomic batch)
                long startSequence = persistResult.NewSequenceNumber - domainResult.Events.Count + 1;
                var unpublishedRecord = new UnpublishedEventsRecord(
                    command.CorrelationId,
                    startSequence,
                    persistResult.NewSequenceNumber,
                    domainResult.Events.Count,
                    command.CommandType,
                    domainResult.IsRejection,
                    DateTimeOffset.UtcNow,
                    RetryCount: 0,
                    LastFailureReason: publishResult.FailureReason);
                await StoreDrainRecordAndRegisterReminderAsync(command.CorrelationId, unpublishedRecord)
                    .ConfigureAwait(false);

                try {
                    await StateManager.SaveStateAsync().ConfigureAwait(false);
                }
                catch (InvalidOperationException ex) {
                    throw new ConcurrencyConflictException(
                        command.CorrelationId,
                        command.AggregateId,
                        command.TenantId,
                        conflictSource: "StateStore",
                        innerException: ex);
                }

                // Story 4.4: Register drain reminder AFTER successful commit
                await RegisterDrainReminderAsync(command.CorrelationId).ConfigureAwait(false);

                LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);
                await WriteAdvisoryStatusAsync(command, CommandStatus.PublishFailed).ConfigureAwait(false);

                _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "PublishFailed"));
                return failResult;
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period) {
        ArgumentNullException.ThrowIfNull(reminderName);

        if (!reminderName.StartsWith("drain-unpublished-", StringComparison.Ordinal)) {
            logger.LogWarning(
                "Unknown reminder ignored: ReminderName={ReminderName}, ActorId={ActorId}",
                reminderName,
                Host.Id);
            return;
        }

        string correlationId = reminderName["drain-unpublished-".Length..];
        await DrainUnpublishedEventsAsync(correlationId).ConfigureAwait(false);
    }

    private async Task DrainUnpublishedEventsAsync(string correlationId) {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsDrain);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain));
        _ = (activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId));

        // Load the unpublished events record
        ConditionalValue<UnpublishedEventsRecord> recordResult = await StateManager
            .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(correlationId))
            .ConfigureAwait(false);

        if (!recordResult.HasValue) {
            // Orphaned reminder -- record was already drained or removed
            logger.LogWarning(
                "Drain record not found (orphaned reminder): CorrelationId={CorrelationId}, ActorId={ActorId}",
                correlationId,
                Host.Id);

            try {
                await UnregisterReminderAsync(UnpublishedEventsRecord.GetReminderName(correlationId))
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogWarning(
                    ex,
                    "Failed to unregister orphaned drain reminder: CorrelationId={CorrelationId}",
                    correlationId);
            }

            return;
        }

        UnpublishedEventsRecord record = recordResult.Value;
        _ = (activity?.SetTag("eventstore.retry_count", record.RetryCount));
        _ = (activity?.SetTag(EventStoreActivitySource.TagEventCount, record.EventCount));

        logger.LogInformation(
            "Drain attempt starting: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}",
            correlationId,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            record.RetryCount,
            record.EventCount);

        try {
            // Load exact persisted event range for this failed command
            IReadOnlyList<EventEnvelope> events = await LoadPersistedEventsRangeAsync(
                identity,
                record.StartSequence,
                record.EndSequence)
                .ConfigureAwait(false);

            // Re-publish events
            EventPublishResult publishResult = await eventPublisher
                .PublishEventsAsync(identity, events, correlationId)
                .ConfigureAwait(false);

            if (publishResult.Success) {
                // Success: remove record, unregister reminder, update advisory status
                await StateManager.RemoveStateAsync(UnpublishedEventsRecord.GetStateKey(correlationId))
                    .ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);

                try {
                    await UnregisterReminderAsync(UnpublishedEventsRecord.GetReminderName(correlationId))
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    logger.LogWarning(
                        ex,
                        "Failed to unregister drain reminder after success: CorrelationId={CorrelationId}",
                        correlationId);
                }

                // Advisory status: Completed or Rejected based on event type
                CommandStatus drainStatus = record.IsRejection ? CommandStatus.Rejected : CommandStatus.Completed;
                try {
                    await commandStatusStore.WriteStatusAsync(
                        identity.TenantId,
                        correlationId,
                        new CommandStatusRecord(
                            drainStatus,
                            DateTimeOffset.UtcNow,
                            identity.AggregateId,
                            EventCount: record.EventCount,
                            RejectionEventType: null,
                            FailureReason: null,
                            TimeoutDuration: null)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    // Rule #12: Advisory status writes -- failure logged, never thrown.
                    logger.LogWarning(
                        ex,
                        "Advisory status write failed after drain success: CorrelationId={CorrelationId}, Status={Status}",
                        correlationId,
                        drainStatus);
                }

                logger.LogInformation(
                    "Drain succeeded: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}",
                    correlationId,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    record.RetryCount,
                    record.EventCount);

                _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            }
            else {
                // Failure: increment retry, save updated record, reminder continues
                UnpublishedEventsRecord updatedRecord = record.IncrementRetry(publishResult.FailureReason);
                await StateManager.SetStateAsync(
                    UnpublishedEventsRecord.GetStateKey(correlationId),
                    updatedRecord).ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);

                logger.LogWarning(
                    "Drain failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}",
                    correlationId,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    updatedRecord.RetryCount,
                    updatedRecord.EventCount);

                _ = (activity?.SetStatus(ActivityStatusCode.Error, publishResult.FailureReason));
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            // Drain infrastructure failure: increment retry, save, reminder continues
            UnpublishedEventsRecord updatedRecord = record.IncrementRetry(ex.Message);
            await StateManager.SetStateAsync(
                UnpublishedEventsRecord.GetStateKey(correlationId),
                updatedRecord).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);

            logger.LogWarning(
                ex,
                "Drain failed with exception: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                updatedRecord.RetryCount);

            _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
        }
    }

    private AggregateIdentity GetAggregateIdentityFromActorId() {
        string actorId = Host.Id.GetId();
        string[] parts = actorId.Split(':', 3);
        if (parts.Length != 3) {
            logger.LogError(
                "Cannot parse actor ID into AggregateIdentity: ActorId={ActorId}",
                actorId);

            throw new InvalidOperationException(
                $"Cannot parse actor ID into AggregateIdentity: {actorId}");
        }

        return new AggregateIdentity(parts[0], parts[1], parts[2]);
    }

    private async Task StoreDrainRecordAndRegisterReminderAsync(
        string correlationId,
        UnpublishedEventsRecord record) =>
        // Stage the drain record (committed with the same SaveStateAsync batch)
        await StateManager.SetStateAsync(
            UnpublishedEventsRecord.GetStateKey(correlationId),
            record).ConfigureAwait(false);

    private async Task RegisterDrainReminderAsync(string correlationId) {
        try {
            (TimeSpan dueTime, TimeSpan period) = GetDrainReminderSchedule();
            _ = await RegisterReminderAsync(
                UnpublishedEventsRecord.GetReminderName(correlationId),
                null,
                dueTime,
                period).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogWarning(
                ex,
                "Drain reminder registration failed: CorrelationId={CorrelationId}. Manual recovery may be needed.",
                correlationId);
        }
    }

    private (TimeSpan DueTime, TimeSpan Period) GetDrainReminderSchedule() {
        EventDrainOptions options = drainOptions.Value;

        TimeSpan dueTime = options.InitialDrainDelay;
        if (dueTime < TimeSpan.Zero) {
            logger.LogWarning(
                "Invalid EventStore:Drain:InitialDrainDelay value {InitialDrainDelay}; defaulting to zero.",
                dueTime);
            dueTime = TimeSpan.Zero;
        }

        TimeSpan period = options.DrainPeriod;
        if (period <= TimeSpan.Zero) {
            logger.LogWarning(
                "Invalid EventStore:Drain:DrainPeriod value {DrainPeriod}; defaulting to 00:01:00.",
                period);
            period = TimeSpan.FromMinutes(1);
        }

        TimeSpan maxPeriod = options.MaxDrainPeriod;
        if (maxPeriod <= TimeSpan.Zero) {
            logger.LogWarning(
                "Invalid EventStore:Drain:MaxDrainPeriod value {MaxDrainPeriod}; defaulting to 00:30:00.",
                maxPeriod);
            maxPeriod = TimeSpan.FromMinutes(30);
        }

        if (period > maxPeriod) {
            logger.LogWarning(
                "EventStore:Drain:DrainPeriod ({DrainPeriod}) exceeds MaxDrainPeriod ({MaxDrainPeriod}); clamping to max.",
                period,
                maxPeriod);
            period = maxPeriod;
        }

        return (dueTime, period);
    }

    private static void SetActivityTags(Activity? activity, CommandEnvelope command) {
        if (activity is null) {
            return;
        }

        _ = activity.SetTag(EventStoreActivitySource.TagCorrelationId, command.CorrelationId);
        _ = activity.SetTag(EventStoreActivitySource.TagTenantId, command.TenantId);
        _ = activity.SetTag(EventStoreActivitySource.TagDomain, command.Domain);
        _ = activity.SetTag(EventStoreActivitySource.TagAggregateId, command.AggregateId);
        _ = activity.SetTag(EventStoreActivitySource.TagCommandType, command.CommandType);
    }

    private static string GetEventTypeName(Hexalith.EventStore.Contracts.Events.IEventPayload eventPayload) =>
        eventPayload is Hexalith.EventStore.Contracts.Events.ISerializedEventPayload serializedPayload
            ? serializedPayload.EventTypeName
            : eventPayload.GetType().Name;

    private static bool TryGetFallbackParentContext(CommandEnvelope command, out ActivityContext parentContext) {
        parentContext = default;

        if (command.Extensions is null ||
            !command.Extensions.TryGetValue(TraceParentExtensionKey, out string? traceParent) ||
            string.IsNullOrWhiteSpace(traceParent)) {
            return false;
        }

        _ = command.Extensions.TryGetValue(TraceStateExtensionKey, out string? traceState);
        return ActivityContext.TryParse(traceParent, traceState, out parentContext);
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
        Activity? processActivity,
        long startTicks) {
        int eventCount = existingPipeline.EventCount ?? 0;

        if (eventCount > 0) {
            IReadOnlyList<EventEnvelope> persistedEvents;
            try {
                persistedEvents = await LoadPersistedEventsForResumeAsync(command.AggregateIdentity, eventCount)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogError(
                    ex,
                    "Resume publication preparation failed: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExpectedEventCount={EventCount}",
                    command.CorrelationId,
                    command.TenantId,
                    command.Domain,
                    command.AggregateId,
                    eventCount);

                return await CompletePublishFailedAsync(
                    command,
                    causationId,
                    stateMachine,
                    pipelineKeyPrefix,
                    idempotencyChecker,
                    existingPipeline,
                    "Unable to prepare persisted events for resume publication",
                    persistedEvents: null,
                    processActivity,
                    startTicks).ConfigureAwait(false);
            }

            EventPublishResult publishResult = await eventPublisher
                .PublishEventsAsync(command.AggregateIdentity, persistedEvents, command.CorrelationId)
                .ConfigureAwait(false);

            if (!publishResult.Success) {
                return await CompletePublishFailedAsync(
                    command,
                    causationId,
                    stateMachine,
                    pipelineKeyPrefix,
                    idempotencyChecker,
                    existingPipeline,
                    publishResult.FailureReason,
                    persistedEvents,
                    processActivity,
                    startTicks).ConfigureAwait(false);
            }

            var eventsPublishedState = new PipelineState(
                command.CorrelationId,
                CommandStatus.EventsPublished,
                command.CommandType,
                existingPipeline.StartedAt,
                EventCount: existingPipeline.EventCount,
                RejectionEventType: existingPipeline.RejectionEventType);

            await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsPublishedState).ConfigureAwait(false);

            LogStageTransition(CommandStatus.EventsPublished, command, causationId, startTicks);
            await WriteAdvisoryStatusAsync(command, CommandStatus.EventsPublished).ConfigureAwait(false);
        }

        bool accepted = existingPipeline.RejectionEventType is null;
        string? errorMessage = existingPipeline.RejectionEventType is not null
            ? $"Domain rejection: {existingPipeline.RejectionEventType}"
            : null;

        CommandProcessingResult result = await CompleteTerminalAsync(
            command,
            causationId,
            idempotencyChecker,
            stateMachine,
            pipelineKeyPrefix,
            accepted,
            eventCount,
            errorMessage,
            processActivity,
            startTicks).ConfigureAwait(false);

        logger.LogInformation(
            "Resume completed: Actor {ActorId}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}",
            Host.Id,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);

        return result;
    }

    private async Task<IReadOnlyList<EventEnvelope>> LoadPersistedEventsForResumeAsync(AggregateIdentity identity, int eventCount) {
        if (eventCount <= 0) {
            return [];
        }

        ConditionalValue<AggregateMetadata> metadataResult = await StateManager
            .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
            .ConfigureAwait(false);

        if (!metadataResult.HasValue) {
            throw new InvalidOperationException(
                $"Cannot resume publication without aggregate metadata for {identity.ActorId}.");
        }

        long currentSequence = metadataResult.Value.CurrentSequence;
        long startSequence = currentSequence - eventCount + 1;

        if (startSequence < 1) {
            throw new InvalidOperationException(
                $"Invalid resume event range for {identity.ActorId}: startSequence={startSequence}, currentSequence={currentSequence}, eventCount={eventCount}.");
        }

        EventEnvelope[] events = await ReadEventsRangeAsync(identity, (int)startSequence, eventCount)
            .ConfigureAwait(false);

        return events
            .OrderBy(x => x.SequenceNumber)
            .ToList();
    }

    private async Task<IReadOnlyList<EventEnvelope>> LoadPersistedEventsRangeAsync(
        AggregateIdentity identity,
        long startSequence,
        long endSequence) {
        if (startSequence < 1) {
            throw new InvalidOperationException(
                $"Invalid drain event range for {identity.ActorId}: startSequence={startSequence}, endSequence={endSequence}.");
        }

        if (endSequence < startSequence) {
            throw new InvalidOperationException(
                $"Invalid drain event range for {identity.ActorId}: startSequence={startSequence}, endSequence={endSequence}.");
        }

        int count = checked((int)(endSequence - startSequence + 1));
        if (count == 0) {
            return [];
        }

        EventEnvelope[] events = await ReadEventsRangeAsync(identity, (int)startSequence, count)
            .ConfigureAwait(false);

        return events
            .OrderBy(x => x.SequenceNumber)
            .ToList();
    }

    private async Task<CommandProcessingResult> CompletePublishFailedAsync(
        CommandEnvelope command,
        string causationId,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        IdempotencyChecker idempotencyChecker,
        PipelineState existingPipeline,
        string? failureReason,
        IReadOnlyList<EventEnvelope>? persistedEvents,
        Activity? processActivity,
        long startTicks) {
        var publishFailedState = new PipelineState(
            command.CorrelationId,
            CommandStatus.PublishFailed,
            command.CommandType,
            existingPipeline.StartedAt,
            EventCount: existingPipeline.EventCount,
            RejectionEventType: existingPipeline.RejectionEventType);
        await stateMachine.CheckpointAsync(pipelineKeyPrefix, publishFailedState).ConfigureAwait(false);

        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        var failResult = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: $"Event publication failed: {failureReason}",
            CorrelationId: command.CorrelationId,
            EventCount: existingPipeline.EventCount ?? 0);

        await idempotencyChecker.RecordAsync(causationId, failResult).ConfigureAwait(false);

        // Story 4.4: Store drain record for recovery on resume path (committed in same atomic batch)
        int eventCount = existingPipeline.EventCount ?? 0;
        bool shouldRegisterReminder = false;
        if (eventCount > 0) {
            bool hasRange = false;
            long startSequence = 0;
            long endSequence = 0;

            if (persistedEvents is { Count: > 0 }) {
                startSequence = persistedEvents.Min(e => e.SequenceNumber);
                endSequence = persistedEvents.Max(e => e.SequenceNumber);
                hasRange = true;
            }
            else {
                ConditionalValue<AggregateMetadata> metadataResult = await StateManager
                    .TryGetStateAsync<AggregateMetadata>(command.AggregateIdentity.MetadataKey)
                    .ConfigureAwait(false);

                if (metadataResult.HasValue) {
                    endSequence = metadataResult.Value.CurrentSequence;
                    startSequence = endSequence - eventCount + 1;
                    hasRange = true;
                }
            }

            if (hasRange
                && startSequence >= 1
                && endSequence >= startSequence
                && (endSequence - startSequence + 1) == eventCount) {
                var unpublishedRecord = new UnpublishedEventsRecord(
                    command.CorrelationId,
                    startSequence,
                    endSequence,
                    eventCount,
                    command.CommandType,
                    existingPipeline.RejectionEventType is not null,
                    DateTimeOffset.UtcNow,
                    RetryCount: 0,
                    LastFailureReason: failureReason);
                await StoreDrainRecordAndRegisterReminderAsync(command.CorrelationId, unpublishedRecord)
                    .ConfigureAwait(false);
                shouldRegisterReminder = true;
            }
            else {
                throw new InvalidOperationException(
                    $"Unable to determine drain sequence range during resume publish failure: CorrelationId={command.CorrelationId}, EventCount={eventCount}, StartSequence={startSequence}, EndSequence={endSequence}.");
            }
        }

        try {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) {
            throw new ConcurrencyConflictException(
                command.CorrelationId,
                command.AggregateId,
                command.TenantId,
                conflictSource: "StateStore",
                innerException: ex);
        }

        // Story 4.4: Register drain reminder AFTER successful commit
        if (shouldRegisterReminder) {
            await RegisterDrainReminderAsync(command.CorrelationId).ConfigureAwait(false);
        }

        LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);
        await WriteAdvisoryStatusAsync(command, CommandStatus.PublishFailed).ConfigureAwait(false);
        LogCommandCompletedSummary(command, causationId, CommandStatus.PublishFailed, startTicks);

        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "PublishFailed"));
        return failResult;
    }

    private async Task<EventEnvelope[]> ReadEventsRangeAsync(
        AggregateIdentity identity,
        int startSequence,
        int count) {
        if (count <= 0) {
            return [];
        }

        var events = new List<EventEnvelope>(count);
        int cursor = startSequence;

        while (cursor < startSequence + count) {
            int batchSize = Math.Min(MaxConcurrentStateReads, startSequence + count - cursor);
            Task<EventEnvelope>[] readTasks = Enumerable.Range(cursor, batchSize)
                .Select(async seq => {
                    ConditionalValue<EventEnvelope> result = await StateManager
                        .TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}{seq}")
                        .ConfigureAwait(false);

                    if (!result.HasValue) {
                        throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
                    }

                    return result.Value;
                })
                .ToArray();

            EventEnvelope[] batch = await Task.WhenAll(readTasks).ConfigureAwait(false);
            events.AddRange(batch);
            cursor += batchSize;
        }

        return [.. events];
    }

    /// <summary>
    /// Story 4.5: Handles infrastructure failures by routing to dead-letter and transitioning to Rejected.
    /// Dead-letter publication is best-effort and non-blocking (AC #7).
    /// Dead-letter publication happens BEFORE SaveStateAsync (task 6.7).
    /// </summary>
    private async Task<CommandProcessingResult> HandleInfrastructureFailureAsync(
        CommandEnvelope command,
        string causationId,
        CommandStatus failureStage,
        Exception exception,
        IdempotencyChecker idempotencyChecker,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        Activity? processActivity,
        long startTicks,
        int? eventCount) {
        Log.InfrastructureFailure(logger, command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, failureStage.ToString(), exception.GetType().Name, exception.Message);

        var deadLetterMessage = DeadLetterMessage.FromException(
            command, failureStage, exception, eventCount);

        // Best-effort dead-letter publication (AC #7) -- BEFORE SaveStateAsync (task 6.7)
        bool published = await deadLetterPublisher
            .PublishDeadLetterAsync(command.AggregateIdentity, deadLetterMessage)
            .ConfigureAwait(false);
        if (!published) {
            logger.LogError(
                "Dead-letter publication failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                command.CorrelationId,
                command.TenantId,
                command.Domain,
                command.AggregateId);
        }

        // Transition to Rejected terminal state
        var rejectedState = new PipelineState(
            command.CorrelationId,
            CommandStatus.Rejected,
            command.CommandType,
            DateTimeOffset.UtcNow,
            EventCount: null,
            RejectionEventType: null);
        await stateMachine.CheckpointAsync(pipelineKeyPrefix, rejectedState).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        var failResult = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: exception.Message,
            CorrelationId: command.CorrelationId,
            EventCount: 0);

        await idempotencyChecker.RecordAsync(causationId, failResult).ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);

        // Advisory status write (non-blocking per rule #12)
        await WriteAdvisoryStatusAsync(command, CommandStatus.Rejected, exception.Message).ConfigureAwait(false);

        LogStageTransition(CommandStatus.Rejected, command, causationId, startTicks);
        LogCommandCompletedSummary(command, causationId, CommandStatus.Rejected, startTicks);
        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "InfrastructureFailure"));
        return failResult;
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
        long startTicks) {
        var result = new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: command.CorrelationId,
            EventCount: eventCount);

        await idempotencyChecker.RecordAsync(causationId, result).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        try {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) {
            throw new ConcurrencyConflictException(
                command.CorrelationId,
                command.AggregateId,
                command.TenantId,
                conflictSource: "StateStore",
                innerException: ex);
        }

        CommandStatus terminalStatus = accepted ? CommandStatus.Completed : CommandStatus.Rejected;
        LogStageTransition(terminalStatus, command, causationId, startTicks);
        await WriteAdvisoryStatusAsync(command, terminalStatus).ConfigureAwait(false);
        LogCommandCompletedSummary(command, causationId, terminalStatus, startTicks);

        _ = (processActivity?.SetStatus(ActivityStatusCode.Ok));
        return result;
    }

    private void LogCommandCompletedSummary(CommandEnvelope command, string causationId, CommandStatus status, long startTicks) {
        double durationMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
        Log.CommandCompletedSummary(
            logger,
            command.CorrelationId,
            causationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            status.ToString(),
            durationMs);
    }

    /// <summary>
    /// Writes advisory command status. Failures are logged at Warning level and never thrown (rule #12).
    /// </summary>
    private async Task WriteAdvisoryStatusAsync(
        CommandEnvelope command,
        CommandStatus status,
        string? failureReason = null) {
        try {
            await commandStatusStore.WriteStatusAsync(
                command.TenantId,
                command.CorrelationId,
                new CommandStatusRecord(
                    status,
                    DateTimeOffset.UtcNow,
                    command.AggregateId,
                    EventCount: null,
                    RejectionEventType: null,
                    FailureReason: failureReason,
                    TimeoutDuration: null)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
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
    private void LogStageTransition(CommandStatus stage, CommandEnvelope command, string causationId, long startTicks) {
        double durationMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
        string stageStr = stage.ToString();

        if (stage == CommandStatus.Rejected) {
            // Domain rejection or infrastructure failure terminal: Warning level
            Log.StageTransitionWarning(logger, Host.Id.GetId(), stageStr, command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, durationMs);
        }
        else {
            // Normal flow stages: Information level
            Log.StageTransition(logger, Host.Id.GetId(), stageStr, command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, durationMs);
        }
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 2000,
            Level = LogLevel.Debug,
            Message = "Actor activated: ActorId={ActorId}, CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, Stage=ActorActivated")]
        public static partial void ActorActivated(
            ILogger logger,
            string actorId,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType);

        [LoggerMessage(
            EventId = 2001,
            Level = LogLevel.Information,
            Message = "Stage transition: ActorId={ActorId}, Stage={Stage}, CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, DurationMs={DurationMs}")]
        public static partial void StageTransition(
            ILogger logger,
            string actorId,
            string stage,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            double durationMs);

        [LoggerMessage(
            EventId = 2002,
            Level = LogLevel.Warning,
            Message = "Stage transition (rejection): ActorId={ActorId}, Stage={Stage}, CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, DurationMs={DurationMs}")]
        public static partial void StageTransitionWarning(
            ILogger logger,
            string actorId,
            string stage,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            double durationMs);

        [LoggerMessage(
            EventId = 2003,
            Level = LogLevel.Error,
            Message = "Infrastructure failure: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, FailureStage={FailureStage}, ExceptionType={ExceptionType}, ErrorMessage={ErrorMessage}, Stage=InfrastructureFailure")]
        public static partial void InfrastructureFailure(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string failureStage,
            string exceptionType,
            string errorMessage);

        [LoggerMessage(
            EventId = 2004,
            Level = LogLevel.Information,
            Message = "Command completed summary: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, Status={Status}, DurationMs={DurationMs}, Stage=CommandCompleted")]
        public static partial void CommandCompletedSummary(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string status,
            double durationMs);
    }
}
