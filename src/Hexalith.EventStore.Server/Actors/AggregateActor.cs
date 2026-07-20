
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Dapr;
using Dapr.Actors.Runtime;

using Grpc.Core;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ContractEventEnvelope = Hexalith.EventStore.Contracts.Events.EventEnvelope;
using ContractEventMetadata = Hexalith.EventStore.Contracts.Events.EventMetadata;

namespace Hexalith.EventStore.Server.Actors;
/// <summary>
/// Aggregate actor -- thin orchestrator for command processing.
/// Story 3.2: Implements 5-step delegation pipeline.
/// Steps 1-4 (idempotency, tenant validation, state rehydration, domain invocation) are real.
/// Step 5: Event persistence (Story 3.7). Step 5b: Snapshot creation (Story 3.9).
/// Story 3.10: Step 3 now loads snapshot FIRST, passes to EventStreamReader for tail-only reads.
/// Story 3.11: Checkpointed state machine, OpenTelemetry activities, advisory status writes.
/// Story 4.2: IRemindable for drain recovery of unpublished events after pub/sub outage.
/// Dead-letter routing handles infrastructure failures at Steps 3-5.
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
    IEventPayloadProtectionService payloadProtectionService,
    ICommandStatusStore commandStatusStore,
    IEventPublisher eventPublisher,
    IOptions<EventDrainOptions> drainOptions,
    IOptions<BackpressureOptions> backpressureOptions,
    IDeadLetterPublisher deadLetterPublisher,
    IServiceProvider? serviceProvider = null,
    ICommandAggregateTypeResolver? commandAggregateTypeResolver = null,
    IOptions<CommandConcurrencyOptions>? concurrencyOptions = null,
    IGlobalPositionAllocator? globalPositionAllocator = null,
    IOptions<IdempotencyRetentionOptions>? idempotencyRetentionOptions = null,
    TimeProvider? timeProvider = null)
    : Actor(host), IAggregateActor, IRemindable {
    private const string TraceParentExtensionKey = "traceparent";
    private const string TraceStateExtensionKey = "tracestate";
    private const string PendingCommandCountKey = "pending_command_count";

    private int MaxPersistenceConflictRetries
        => Math.Max(
            0,
            concurrencyOptions?.Value.MaxPersistenceConflictRetries
            ?? CommandConcurrencyOptions.DefaultMaxPersistenceConflictRetries);

    private int IdempotencyRetentionSeconds
        => idempotencyRetentionOptions?.Value.TerminalRetentionSeconds
            ?? IdempotencyRetentionOptions.DefaultTerminalRetentionSeconds;

    private TimeProvider IdempotencyTimeProvider { get; } = timeProvider ?? TimeProvider.System;
    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
        => ProcessCommandAsync(command, CancellationToken.None);

    /// <summary>
    /// Processes a command envelope within the aggregate actor context.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="cancellationToken">Cancellation token for local/in-process callers.</param>
    /// <returns>The result of processing the command.</returns>
    public async Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

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
            bool pendingCommandTracked = false;
            bool drainRecordCreated = false;

            string causationId = string.IsNullOrWhiteSpace(command.CausationId)
                ? command.MessageId
                : command.CausationId;
            var commandIdentity = new CommandProcessingIdentity(
                command.MessageId,
                causationId,
                command.CommandType);

            Log.ActorActivated(logger, Host.Id.GetId(), command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType);

            // SEC-2 CRITICAL: tenant ownership is validated before any actor-state helper is
            // created or invoked. A mismatched tenant must not learn whether command state exists.
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.TenantValidation,
                ActivityKind.Internal))
            {
                SetActivityTags(activity, command);

                var tenantValidator = new TenantValidator(
                    Host.LoggerFactory.CreateLogger<TenantValidator>());
                try
                {
                    tenantValidator.Validate(command.TenantId, Host.Id.GetId());
                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }
                catch (TenantMismatchException ex)
                {
                    logger.LogWarning(
                        "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, CommandTenant={CommandTenant}, ActorTenant={ActorTenant}",
                        "TenantMismatch",
                        command.CorrelationId,
                        ex.CommandTenant,
                        ex.ActorTenant);

                    _ = (activity?.AddException(ex));
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch"));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "TenantMismatch"));
                    return new CommandProcessingResult(
                        Accepted: false,
                        ErrorMessage: ex.Message,
                        CorrelationId: command.CorrelationId);
                }
            }

            // Per-call helpers (require actor's IActorStateManager)
            var idempotencyChecker = new IdempotencyChecker(
                StateManager,
                Host.LoggerFactory.CreateLogger<IdempotencyChecker>(),
                IdempotencyTimeProvider);
            var stateMachine = new ActorStateMachine(
                StateManager,
                Host.LoggerFactory.CreateLogger<ActorStateMachine>());
            string pipelineKeyPrefix = command.AggregateIdentity.PipelineKeyPrefix;

            // Idempotency check follows tenant validation and uses exact command identity.
            using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                EventStoreActivitySource.IdempotencyCheck,
                ActivityKind.Internal)) {
                SetActivityTags(activity, command);

                IdempotencyCheckResult idempotencyCheck = await idempotencyChecker
                    .CheckAsync(commandIdentity)
                    .ConfigureAwait(false);

                if (idempotencyCheck.StateMutationStaged)
                {
                    await StateManager.SaveStateAsync().ConfigureAwait(false);
                }

                if (idempotencyCheck.Outcome is IdempotencyCheckOutcome.ExactTerminalDuplicate
                    or IdempotencyCheckOutcome.LegacyMigration
                    or IdempotencyCheckOutcome.RetryableRecoverable)
                {
                    CommandProcessingResult cached = idempotencyCheck.Result
                        ?? throw new InvalidOperationException("Cached idempotency outcome did not contain a result.");
                    logger.LogInformation(
                        "Duplicate command detected: MessageId={MessageId}, CorrelationId={CorrelationId}, ActorId={ActorId}. Returning cached result.",
                        command.MessageId,
                        command.CorrelationId,
                        Host.Id);
                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Ok));
                    return cached;
                }

                if (idempotencyCheck.Outcome == IdempotencyCheckOutcome.IdentityConflict)
                {
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                    return new CommandProcessingResult(
                        Accepted: false,
                        ErrorMessage: "command_identity_conflict",
                        CorrelationId: command.CorrelationId);
                }

                if (idempotencyCheck.Outcome == IdempotencyCheckOutcome.Expired)
                {
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "IdempotencyKeyExpired"));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "IdempotencyKeyExpired"));
                    return new CommandProcessingResult(
                        Accepted: false,
                        ErrorMessage: "idempotency_key_expired",
                        CorrelationId: command.CorrelationId);
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

                    bool exactIdentity = commandIdentity.Matches(existingPipeline);
                    bool committedCheckpoint = CanRepresentCommittedEvents(existingPipeline);

                    if (exactIdentity && committedCheckpoint)
                    {
                        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                        return await ResumeFromEventsStoredAsync(
                            command, causationId, existingPipeline, idempotencyChecker, stateMachine,
                            pipelineKeyPrefix, processActivity, startTicks).ConfigureAwait(false);
                    }

                    if (!exactIdentity && committedCheckpoint)
                    {
                        // A stale committed checkpoint that lacks a persisted event range (legacy,
                        // pre-range) cannot be handed off safely: its events cannot be identified
                        // without re-deriving from the mutable stream head. Fail closed and preserve it.
                        bool missingCommittedRange = existingPipeline.EventCount is > 0
                            && (existingPipeline.StartSequence is null || existingPipeline.EndSequence is null);

                        if (!HasCompletePipelineIdentity(existingPipeline)
                            || string.Equals(existingPipeline.MessageId, command.MessageId, StringComparison.Ordinal)
                            || missingCommittedRange)
                        {
                            Log.PipelineIdentityConflict(
                                logger,
                                command.CorrelationId,
                                command.MessageId,
                                existingPipeline.CurrentStage.ToString());
                            _ = (activity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            return new CommandProcessingResult(
                                Accepted: false,
                                ErrorMessage: "command_identity_conflict",
                                CorrelationId: command.CorrelationId);
                        }

                        try
                        {
                            await HandoffStaleCommittedCheckpointAsync(
                                existingPipeline,
                                stateMachine,
                                pipelineKeyPrefix).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // A handoff that cannot complete must not fault the actor turn, which DAPR
                            // would redeliver into the same fault (poison loop). Discard any uncommitted
                            // staged state, preserve the checkpoint, and fail closed on the incoming command.
                            await StateManager.ClearCacheAsync().ConfigureAwait(false);
                            logger.LogError(
                                ex,
                                "Stale committed checkpoint handoff failed: CorrelationId={CorrelationId}, MessageId={MessageId}, Stage={Stage}",
                                command.CorrelationId,
                                command.MessageId,
                                existingPipeline.CurrentStage);
                            _ = (activity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            return new CommandProcessingResult(
                                Accepted: false,
                                ErrorMessage: "command_identity_conflict",
                                CorrelationId: command.CorrelationId);
                        }
                    }
                    else
                    {
                        if (existingPipeline.CurrentStage != CommandStatus.Processing)
                        {
                            Log.PipelineIdentityConflict(
                                logger,
                                command.CorrelationId,
                                command.MessageId,
                                existingPipeline.CurrentStage.ToString());
                            _ = (activity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                            return new CommandProcessingResult(
                                Accepted: false,
                                ErrorMessage: "command_identity_conflict",
                                CorrelationId: command.CorrelationId);
                        }

                        // A Processing checkpoint has no committed events. Replace its pending slot
                        // with the incoming command after committing cleanup.
                        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                            .ConfigureAwait(false);
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                        pendingCommandTracked = true;
                    }
                }

                _ = (activity?.SetStatus(ActivityStatusCode.Ok));
            }

            try {
                // Step 2b: Backpressure check (Story 4.3, FR67)
                // Runs after tenant validation to preserve the existing security invariant that
                // no actor state is read before tenant isolation is confirmed.
                if (!pendingCommandTracked) {
                    using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                        EventStoreActivitySource.BackpressureCheck,
                        ActivityKind.Internal);
                    SetActivityTags(activity, command);

                    int pendingCount;
                    try {
                        pendingCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) {
                        // Fail-open: if state read fails, allow command through (availability > backpressure)
                        logger.LogWarning(
                            ex,
                            "Backpressure check state read failed (fail-open): ActorId={ActorId}, CorrelationId={CorrelationId}. Allowing command through.",
                            Host.Id,
                            command.CorrelationId);
                        pendingCount = 0;
                    }

                    BackpressureOptions bpOptions = backpressureOptions.Value;
                    if (pendingCount >= bpOptions.MaxPendingCommandsPerAggregate) {
                        Log.BackpressureRejected(
                            logger,
                            Host.Id.GetId(),
                            command.CorrelationId,
                            command.TenantId,
                            command.Domain,
                            command.AggregateId,
                            pendingCount,
                            bpOptions.MaxPendingCommandsPerAggregate);

                        _ = (activity?.SetStatus(ActivityStatusCode.Error, "BackpressureExceeded"));
                        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "BackpressureExceeded"));
                        return new CommandProcessingResult(
                            Accepted: false,
                            ErrorMessage: $"Backpressure exceeded: {pendingCount} pending commands (threshold: {bpOptions.MaxPendingCommandsPerAggregate})",
                            CorrelationId: command.CorrelationId,
                            BackpressureExceeded: true,
                            BackpressurePendingCount: pendingCount,
                            BackpressureThreshold: bpOptions.MaxPendingCommandsPerAggregate);
                    }

                    await StagePendingCommandCountAsync(pendingCount + 1).ConfigureAwait(false);
                    pendingCommandTracked = true;

                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                }

                // Checkpoint Processing stage (AC #1, #7)
                var pipelineState = new PipelineState(
                    command.CorrelationId,
                    CommandStatus.Processing,
                    command.CommandType,
                    DateTimeOffset.UtcNow,
                    EventCount: null,
                    RejectionEventType: null,
                    ResultPayload: null,
                    MessageId: commandIdentity.MessageId,
                    CausationId: commandIdentity.CausationId);
                await stateMachine.CheckpointAsync(pipelineKeyPrefix, pipelineState).ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);

                LogStageTransition(CommandStatus.Processing, command, causationId, startTicks);
                await WriteAdvisoryStatusAsync(command, CommandStatus.Processing).ConfigureAwait(false);

                int persistenceConflictRetryCount = 0;
                int maxPersistenceConflictRetries = MaxPersistenceConflictRetries;

                RetryAfterPersistenceConflict:
                // Step 3: State rehydration (Story 3.10 -- snapshot-first flow)
                // Dead-letter routing handles infrastructure exceptions.
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

                        // Story 22.7b: pre-domain readability boundary. Unprotect every rehydrated
                        // event BEFORE constructing DomainServiceCurrentState so domain services
                        // never receive protected bytes through ToContractEventEnvelope. Any
                        // ProviderOpaque envelope or Unreadable provider outcome throws the typed
                        // ProtectedDataUnreadableException which is caught below and routed via the
                        // existing dead-letter path. OperationCanceledException continues to
                        // propagate unchanged.
                        IReadOnlyList<EventEnvelope> readableEvents = rehydrationResult is null
                            ? []
                            : await EnsureEventsReadableForDomainAsync(
                                command.AggregateIdentity,
                                rehydrationResult.Events,
                                cancellationToken).ConfigureAwait(false);

                        currentState = rehydrationResult is null
                            ? null
                            : new DomainServiceCurrentState(
                                rehydrationResult.SnapshotState,
                                [.. readableEvents.Select(ToContractEventEnvelope)],
                                rehydrationResult.LastSnapshotSequence,
                                rehydrationResult.CurrentSequence);

                        logger.LogInformation(
                            "State rehydrated: {StateType} for ActorId={ActorId}, CorrelationId={CorrelationId}",
                            currentState?.GetType().Name ?? "null",
                            Host.Id,
                            command.CorrelationId);

                        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) {
                        ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "rehydrate");
                        // State rehydration infrastructure failure -- dead-letter routing
                        return await HandleInfrastructureFailureAsync(
                            command, causationId, CommandStatus.Processing, ex,
                            stateMachine, pipelineKeyPrefix,
                            processActivity, startTicks, eventCount: null).ConfigureAwait(false);
                    }
                }

                // Step 4: Domain service invocation (Story 3.5)
                // Dead-letter routing handles infrastructure exceptions.
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
                        ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "domain-service-invoke");
                        // Domain service invocation infrastructure failure -- dead-letter routing
                        return await HandleInfrastructureFailureAsync(
                            command, causationId, CommandStatus.Processing, ex,
                            stateMachine, pipelineKeyPrefix,
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
                // Dead-letter routing handles infrastructure exceptions.
                EventPersistResult persistResult;
                using (Activity? activity = EventStoreActivitySource.Instance.StartActivity(
                    EventStoreActivitySource.EventsPersist,
                    ActivityKind.Internal)) {
                    SetActivityTags(activity, command);

                    try {
                        var eventPersister = new EventPersister(
                            StateManager,
                            Host.LoggerFactory.CreateLogger<EventPersister>(),
                            payloadProtectionService,
                            globalPositionAllocator);

                        string aggregateType = await ResolveAggregateTypeAsync(command, cancellationToken).ConfigureAwait(false);

                        persistResult = await eventPersister
                            .PersistEventsAsync(
                                identity: command.AggregateIdentity,
                                aggregateType: aggregateType,
                                command: command,
                                domainResult: domainResult,
                                domainServiceVersion: domainServiceVersion)
                            .ConfigureAwait(false);

                        // Step 5b: Snapshot creation (Story 3.9)
                        if (persistResult.NewSequenceNumber > 0 && currentState is not null) {
                            bool shouldSnapshot = await snapshotManager
                                .ShouldCreateSnapshotAsync(command.TenantId, command.Domain, aggregateType, persistResult.NewSequenceNumber, lastSnapshotSequence)
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
                            RejectionEventType: rejectionEventType,
                            MessageId: commandIdentity.MessageId,
                            CausationId: commandIdentity.CausationId,
                            StartSequence: persistResult.NewSequenceNumber - domainResult.Events.Count + 1,
                            EndSequence: persistResult.NewSequenceNumber);
                        await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsStoredState).ConfigureAwait(false);

                        // Atomic commit: events + snapshot + EventsStored checkpoint (AC #9)
                        try {
                            await StateManager.SaveStateAsync().ConfigureAwait(false);
                        }
                        catch (InvalidOperationException ex) {
                            var conflict = new ConcurrencyConflictException(
                                command.CorrelationId,
                                command.AggregateId,
                                command.TenantId,
                                conflictSource: "StateStore",
                                innerException: ex,
                                messageId: command.MessageId);

                            if (persistenceConflictRetryCount < maxPersistenceConflictRetries) {
                                persistenceConflictRetryCount++;
                                Log.PersistenceConflictRetry(
                                    logger,
                                    command.CorrelationId,
                                    causationId,
                                    command.TenantId,
                                    command.Domain,
                                    command.AggregateId,
                                    command.CommandType,
                                    persistenceConflictRetryCount,
                                    maxPersistenceConflictRetries);

                                await StateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
                                goto RetryAfterPersistenceConflict;
                            }

                            return await CompleteConcurrencyConflictAsync(
                                command,
                                causationId,
                                conflict,
                                stateMachine,
                                pipelineKeyPrefix,
                                processActivity,
                                startTicks,
                                maxPersistenceConflictRetries).ConfigureAwait(false);
                        }

                        LogStageTransition(CommandStatus.EventsStored, command, causationId, startTicks);
                        await WriteAdvisoryStatusAsync(command, CommandStatus.EventsStored).ConfigureAwait(false);

                        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyConflictException) {
                        ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "event-persist");
                        // Event persistence infrastructure failure -- dead-letter routing
                        return await HandleInfrastructureFailureAsync(
                            command, causationId, CommandStatus.EventsStored, ex,
                            stateMachine, pipelineKeyPrefix,
                            processActivity, startTicks, eventCount: domainResult.Events.Count).ConfigureAwait(false);
                    }
                }

                // Story 4.1: Publish events via DAPR pub/sub with CloudEvents 1.0
                // Rejection events ARE published (D3: rejection events are normal events).
                EventPublishResult publishResult = await eventPublisher
                    .PublishEventsAsync(
                        command.AggregateIdentity,
                        persistResult.PersistedEnvelopes,
                        command.CorrelationId,
                        triggerProjectionUpdate: false)
                    .ConfigureAwait(false);

                if (publishResult.Success) {
                    // Checkpoint EventsPublished
                    var eventsPublishedState = new PipelineState(
                        command.CorrelationId,
                        CommandStatus.EventsPublished,
                        command.CommandType,
                        pipelineState.StartedAt,
                        EventCount: domainResult.Events.Count,
                        RejectionEventType: domainResult.IsRejection ? GetEventTypeName(domainResult.Events[0]) : null,
                        MessageId: commandIdentity.MessageId,
                        CausationId: commandIdentity.CausationId,
                        StartSequence: persistResult.NewSequenceNumber - domainResult.Events.Count + 1,
                        EndSequence: persistResult.NewSequenceNumber);
                    await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsPublishedState).ConfigureAwait(false);

                    LogStageTransition(CommandStatus.EventsPublished, command, causationId, startTicks);
                    await WriteAdvisoryStatusAsync(command, CommandStatus.EventsPublished).ConfigureAwait(false);

                    // Terminal state: Completed (or Rejected advisory)
                    bool accepted = !domainResult.IsRejection;
                    string? rejectionType = domainResult.IsRejection
                        ? GetEventTypeName(domainResult.Events[0])
                        : null;
                    string? errorMessage = rejectionType is not null
                        ? $"Domain rejection: {rejectionType}"
                        : null;

                    return await CompleteTerminalAsync(
                        command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        accepted, domainResult.Events.Count, errorMessage,
                        processActivity, startTicks,
                        rejectionEventType: rejectionType,
                        resultPayload: domainResult.ResultPayload).ConfigureAwait(false);
                }
                else {
                    // Publication failed: transition to PublishFailed terminal state
                    string? rejectionEventType = domainResult.IsRejection
                        ? GetEventTypeName(domainResult.Events[0])
                        : null;

                    var publishFailedState = new PipelineState(
                        command.CorrelationId,
                        CommandStatus.PublishFailed,
                        command.CommandType,
                        pipelineState.StartedAt,
                        EventCount: domainResult.Events.Count,
                        RejectionEventType: rejectionEventType,
                        MessageId: commandIdentity.MessageId,
                        CausationId: commandIdentity.CausationId,
                        StartSequence: persistResult.NewSequenceNumber - domainResult.Events.Count + 1,
                        EndSequence: persistResult.NewSequenceNumber);
                    await stateMachine.CheckpointAsync(pipelineKeyPrefix, publishFailedState).ConfigureAwait(false);

                    // Cleanup pipeline and commit atomically
                    await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                        .ConfigureAwait(false);

                    CommandProcessingResult failResult = CreatePublishFailedResult(
                        command.CorrelationId,
                        domainResult.Events.Count,
                        publishResult.FailureReason,
                        rejectionEventType);

                    await RecordIdempotencyAsync(
                        idempotencyChecker,
                        commandIdentity,
                        failResult,
                        IdempotencyRecordDisposition.Recoverable).ConfigureAwait(false);

                    // Story 4.2: Store drain record for recovery (committed in same atomic batch)
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
                        LastFailureReason: publishResult.FailureReason,
                        MessageId: command.MessageId);
                    await StoreDrainRecordAndRegisterReminderAsync(command.MessageId, unpublishedRecord)
                        .ConfigureAwait(false);

                    // Story 4.3: Mark drain record created so try/finally skips decrement
                    drainRecordCreated = true;

                    try {
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                    }
                    catch (InvalidOperationException ex) {
                        throw new ConcurrencyConflictException(
                            command.CorrelationId,
                            command.AggregateId,
                            command.TenantId,
                            conflictSource: "StateStore",
                            innerException: ex,
                            messageId: command.MessageId);
                    }

                    // Story 4.2: Register drain reminder AFTER successful commit
                    await RegisterDrainReminderAsync(command.MessageId).ConfigureAwait(false);

                    LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);
                    await WriteAdvisoryStatusAsync(
                        command,
                        CommandStatus.PublishFailed,
                        publishResult.FailureReason,
                        domainResult.Events.Count,
                        rejectionEventType).ConfigureAwait(false);

                    _ = (processActivity?.SetTag("eventstore.publish_failed", true));
                    _ = (processActivity?.SetTag("eventstore.drain_scheduled", true));
                    _ = (processActivity?.SetStatus(
                        failResult.Accepted ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
                        failResult.Accepted ? null : "PublishFailed"));
                    return failResult;
                }

            } // end try (pending command tracking)
            finally {
                // Story 4.3: Decrement counter if incremented and no drain record created.
                // Covers: success, domain rejection, tenant rejection, dead-letter, unhandled exceptions.
                // Skips: backpressure reject (not incremented), idempotent (not incremented),
                //        resume (not incremented), PublishFailed (drain pending).
                if (pendingCommandTracked && !drainRecordCreated) {
                    try {
                        _ = await DecrementPendingCommandCountAsync().ConfigureAwait(false);
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) {
                        logger.LogWarning(
                            ex,
                            "Failed to decrement pending command count in finally block: ActorId={ActorId}, CorrelationId={CorrelationId}",
                            Host.Id,
                            command.CorrelationId);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<EventEnvelope[]> GetEventsAsync(long fromSequence) {
        fromSequence = Math.Max(0, fromSequence);

        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await StateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }

        if (!metadataResult.HasValue) {
            return [];
        }

        long currentSequence = metadataResult.Value.CurrentSequence;
        if (currentSequence <= 0) {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={currentSequence} for {identity.ActorId}");
        }

        if (currentSequence <= fromSequence) {
            return [];
        }

        int startSequence = checked((int)(fromSequence + 1));
        int eventCount = checked((int)(currentSequence - fromSequence));
        string keyPrefix = identity.EventStreamKeyPrefix;

        int endExclusive = startSequence + eventCount;
        var events = new List<EventEnvelope>(eventCount);

        for (int seq = startSequence; seq < endExclusive; seq++) {
            ConditionalValue<EventEnvelope> eventResult;
            try {
                eventResult = await StateManager
                    .TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                throw new EventDeserializationException(seq, identity.ActorId, ex);
            }

            if (!eventResult.HasValue) {
                throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
            }

            events.Add(eventResult.Value);
        }

        return [.. events];
    }

    /// <inheritdoc/>
    public async Task<EventEnvelope[]> ReadEventsRangeAsync(long fromSequence, long? toSequence, int maxCount) {
        // P3: explicit negative guard mirrors the fake's contract; the prior `Math.Max(0, ...)`
        // silently coerced negatives, hiding caller bugs.
        ArgumentOutOfRangeException.ThrowIfNegative(fromSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        if (toSequence.HasValue && toSequence.Value <= fromSequence) {
            return [];
        }

        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        // P4: read metadata BEFORE the overflow guard so the empty-stream contract
        // (return []) is honored even when fromSequence is at extreme boundary values.
        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await StateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsDeserializationFailure(ex)) {
            // P2: narrow filter — only deserialization-related exceptions are reclassified
            // as EventDeserializationException with the -1 sentinel. Programmer errors
            // (NRE/OOM/InvalidOperation/KeyNotFound) propagate as 500 InternalError.
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }

        if (!metadataResult.HasValue) {
            return [];
        }

        long currentSequence = metadataResult.Value.CurrentSequence;
        if (currentSequence < 0) {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={currentSequence} for {identity.ActorId}");
        }

        if (currentSequence <= fromSequence) {
            return [];
        }

        // Caller passing toSequence == long.MaxValue against an empty stream should still receive
        // the empty-page contract; validate the upper bound only once events may be read.
        if (toSequence is long ts && ts > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(toSequence), "toSequence must be <= int.MaxValue.");
        }

        // P19-7P (pass-7 MEDIUM): only refuse when `fromSequence` itself is at the int boundary
        // (no room to compute `fromSequence + 1`). The prior guard `fromSequence > int.MaxValue -
        // maxCount` was overly conservative — for `fromSequence = int.MaxValue - 100, maxCount =
        // 200, currentSequence = int.MaxValue - 50` we have 50 events to read, but the old guard
        // refused. The available-count clamp below already bounds the actual read to whatever the
        // stream contains.
        if (fromSequence >= int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(fromSequence), "Requested event range exceeds the supported sequence boundary.");
        }

        long upperBound = Math.Min(toSequence ?? currentSequence, currentSequence);
        long availableCount = upperBound - fromSequence;
        // Clamp count to the int range explicitly — `Math.Min(availableCount, maxCount)` can still
        // exceed int when callers pass very large maxCount near boundary fromSequence values.
        long boundedAvailable = Math.Min(availableCount, int.MaxValue - fromSequence);
        int count = checked((int)Math.Min(boundedAvailable, maxCount));
        int startSequence = checked((int)(fromSequence + 1));
        return await ReadEventsRangeAsync(identity, startSequence, count).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetCurrentSequenceAsync() {
        AggregateStreamMetadata metadata = await GetStreamMetadataAsync().ConfigureAwait(false);
        return metadata.Exists ? metadata.CurrentSequence : 0;
    }

    /// <inheritdoc/>
    public async Task<AggregateStreamMetadata> GetStreamMetadataAsync() {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await StateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsDeserializationFailure(ex)) {
            // P2: narrow filter — see ReadEventsRangeAsync for rationale.
            throw new EventDeserializationException(-1, identity.ActorId, ex);
        }

        if (!metadataResult.HasValue) {
            return new AggregateStreamMetadata(Exists: false, CurrentSequence: 0);
        }

        long currentSequence = metadataResult.Value.CurrentSequence;
        // P4-8P (pass-8): relax from `<= 0` to `< 0`. Per P-DEC3-7P (pass-7), an Exists=true row
        // with CurrentSequence==0 is a valid "touched but empty" stream (ReadEventsRangeAsync's
        // empty-stream short-circuit returns []). The previous `<= 0` check threw on this state,
        // breaking the controller's StreamMetadataAsync → ReadEventsRangeAsync flow for touched
        // empty streams. Only a negative CurrentSequence remains a corruption indicator.
        if (currentSequence < 0) {
            throw new InvalidOperationException(
                $"Invalid aggregate metadata: CurrentSequence={currentSequence} for {identity.ActorId}");
        }

        return new AggregateStreamMetadata(Exists: true, CurrentSequence: currentSequence);
    }

    /// <inheritdoc/>
    public async Task<ManualSnapshotResult> CreateManualSnapshotAsync(string? correlationId) {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        long currentSequence = 0;

        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            CancellationToken cancellationToken = timeout.Token;

            AggregateStreamMetadata metadata = await GetStreamMetadataAsync().ConfigureAwait(false);
            currentSequence = metadata.CurrentSequence;
            if (!metadata.Exists || metadata.CurrentSequence <= 0) {
                return new ManualSnapshotResult(
                    ManualSnapshotOutcome.NotFound,
                    0,
                    identity.SnapshotKey,
                    "NotFound",
                    "Aggregate stream was not found.");
            }

            SnapshotLoadResult snapshotInspection = await snapshotManager
                .InspectSnapshotForManualOverwriteAsync(identity, StateManager, correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (snapshotInspection.Outcome is SnapshotLoadOutcome.UnreadableProtected
                or SnapshotLoadOutcome.ProviderOpaque
                or SnapshotLoadOutcome.Corrupt) {
                return new ManualSnapshotResult(
                    ManualSnapshotOutcome.UnreadableProtected,
                    metadata.CurrentSequence,
                    identity.SnapshotKey,
                    snapshotInspection.ReasonCode ?? snapshotInspection.Outcome.ToString(),
                    "Existing snapshot cannot be safely read.");
            }

            if (snapshotInspection.Snapshot is not null
                && snapshotInspection.Snapshot.SequenceNumber >= metadata.CurrentSequence) {
                return new ManualSnapshotResult(
                    ManualSnapshotOutcome.AlreadyCurrent,
                    metadata.CurrentSequence,
                    identity.SnapshotKey,
                    null,
                    null);
            }

            var eventStreamReader = new EventStreamReader(
                StateManager,
                Host.LoggerFactory.CreateLogger<EventStreamReader>());

            object? snapshotState = await MaterializeManualSnapshotStateAsync(
                identity,
                eventStreamReader,
                metadata.CurrentSequence,
                correlationId,
                cancellationToken).ConfigureAwait(false);
            if (snapshotState is null) {
                return new ManualSnapshotResult(
                    ManualSnapshotOutcome.InfrastructureFailure,
                    metadata.CurrentSequence,
                    identity.SnapshotKey,
                    "StateReconstructionFailed",
                    "Manual snapshot state could not be reconstructed.");
            }

            await snapshotManager.CreateSnapshotAsync(
                identity,
                metadata.CurrentSequence,
                snapshotState,
                StateManager,
                correlationId,
                cancellationToken,
                throwOnFailure: true).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);

            return new ManualSnapshotResult(
                ManualSnapshotOutcome.Created,
                metadata.CurrentSequence,
                identity.SnapshotKey,
                null,
                null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ProtectedDataUnreadableException ex) {
            return new ManualSnapshotResult(
                ManualSnapshotOutcome.UnreadableProtected,
                ex.SequenceNumber ?? currentSequence,
                identity.SnapshotKey,
                ex.ReasonCode,
                "Protected event data cannot be safely read.");
        }
        catch (Exception ex) {
            logger.LogWarning(
                "Manual snapshot creation failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Reason={Reason}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                ex.GetType().Name);
            return new ManualSnapshotResult(
                ManualSnapshotOutcome.InfrastructureFailure,
                currentSequence,
                identity.SnapshotKey,
                "InfrastructureFailure",
                "Manual snapshot creation failed.");
        }
    }

    private async Task<object?> MaterializeManualSnapshotStateAsync(
        AggregateIdentity identity,
        EventStreamReader eventStreamReader,
        long currentSequence,
        string? correlationId,
        CancellationToken cancellationToken) {
        if (serviceProvider?.GetService(typeof(IAggregateStateReconstructor)) is not IAggregateStateReconstructor reconstructor) {
            logger.LogWarning(
                "Manual snapshot state reconstruction service is unavailable: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId);
            return null;
        }

        RehydrationResult? fullReplay = await eventStreamReader
            .RehydrateAsync(identity)
            .ConfigureAwait(false);
        if (fullReplay is null || fullReplay.Events.Count == 0) {
            return null;
        }

        IReadOnlyList<EventEnvelope> readableEvents = await EnsureEventsReadableForDomainAsync(
            identity,
            fullReplay.Events,
            cancellationToken).ConfigureAwait(false);

        string aggregateType = readableEvents[^1].AggregateType;
        AggregateReconstructionResult reconstruction = await reconstructor
            .ReconstructAsync(
                identity,
                aggregateType,
                readableEvents,
                currentSequence,
                includeTimeline: false,
                requestId: correlationId,
                cancellationToken)
            .ConfigureAwait(false);

        if (reconstruction.Status != AggregateReconstructionStatus.Succeeded
            || reconstruction.LastAppliedSequenceNumber != currentSequence
            || string.IsNullOrWhiteSpace(reconstruction.StateJson)) {
            logger.LogWarning(
                "Manual snapshot state reconstruction failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Status={Status}, LastAppliedSequenceNumber={LastAppliedSequenceNumber}",
                correlationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                reconstruction.Status,
                reconstruction.LastAppliedSequenceNumber);
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(reconstruction.StateJson);
    }

    private const int MaxExceptionFrames = 8;

    // Only treat JSON/serialization failures as data corruption. DAPR exceptions are transport
    // failures and must flow to the service-unavailable path instead.
    private static bool IsDeserializationFailure(Exception exception)
        => IsDeserializationFailure(exception, depth: 0);

    private static bool IsDeserializationFailure(Exception exception, int depth) {
        if (ContainsOperationCanceledException(exception, depth)) {
            return false;
        }

        // P18-7P (pass-7 MEDIUM): short-circuit on OperationCanceledException at every recursion
        // depth. An OCE wrapped inside an AggregateException InnerExceptions[i] must NOT be
        // classified as deserialization (which would consume it as EventDeserializationException
        // and erase the cancellation signal). The depth-0 check already exists for top-level OCE;
        // this extends the contract to nested chains.
        if (depth >= MaxExceptionFrames || exception is OperationCanceledException) {
            return false;
        }

        if (exception is System.Text.Json.JsonException
            or System.IO.InvalidDataException
            or EventDeserializationException) {
            return true;
        }

        // P15-6P: AggregateException carries multiple inner exceptions in InnerExceptions;
        // walking only InnerException loses every branch but the first. A real deserialization
        // failure nested in InnerExceptions[i] would be misclassified as a programmer error.
        if (exception is AggregateException aggregate) {
            // P18-7P (pass-7 MEDIUM): if ANY inner exception is OCE, the entire chain is treated
            // as cancellation, not deserialization. This prevents a JsonException sibling from
            // shadowing the cancellation signal in a Task.WhenAll-style failure.
            foreach (Exception inner in aggregate.InnerExceptions) {
                if (inner is OperationCanceledException) {
                    return false;
                }
            }

            foreach (Exception inner in aggregate.InnerExceptions) {
                if (IsDeserializationFailure(inner, depth + 1)) {
                    return true;
                }
            }

            return false;
        }

        return exception.InnerException is not null && IsDeserializationFailure(exception.InnerException, depth + 1);
    }

    private static bool ContainsOperationCanceledException(Exception exception, int depth) {
        if (depth >= MaxExceptionFrames) {
            return false;
        }

        if (exception is OperationCanceledException) {
            return true;
        }

        if (exception is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                if (ContainsOperationCanceledException(inner, depth + 1)) {
                    return true;
                }
            }

            return false;
        }

        return exception.InnerException is not null && ContainsOperationCanceledException(exception.InnerException, depth + 1);
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

        string trackingId = reminderName["drain-unpublished-".Length..];
        await DrainUnpublishedEventsAsync(trackingId).ConfigureAwait(false);
    }

    private async Task DrainUnpublishedEventsAsync(string trackingId) {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsDrain);
        _ = (activity?.SetTag("eventstore.message_id", trackingId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, identity.TenantId));
        _ = (activity?.SetTag(EventStoreActivitySource.TagDomain, identity.Domain));
        _ = (activity?.SetTag(EventStoreActivitySource.TagAggregateId, identity.AggregateId));

        // Load the unpublished events record
        ConditionalValue<UnpublishedEventsRecord> recordResult;
        try {
            recordResult = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(trackingId))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _ = (activity?.SetTag("eventstore.failure_reason", DrainReasonCodes.StateStoreFailure));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, DrainReasonCodes.StateStoreFailure));
            logger.LogWarning(
                ex,
                "Failed to load drain record from state store: TrackingId={TrackingId}, ActorId={ActorId}",
                trackingId,
                Host.Id);
            throw new DrainStateStoreException("Failed to load drain record from state store.", ex);
        }

        if (!recordResult.HasValue) {
            // Orphaned reminder -- record was already drained or removed
            logger.LogWarning(
                "Drain record not found (orphaned reminder): TrackingId={TrackingId}, ActorId={ActorId}",
                trackingId,
                Host.Id);

            try {
                await UnregisterReminderAsync(UnpublishedEventsRecord.GetReminderName(trackingId))
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                logger.LogWarning(
                    ex,
                    "Failed to unregister orphaned drain reminder: TrackingId={TrackingId}",
                    trackingId);
            }

            return;
        }

        UnpublishedEventsRecord record = recordResult.Value;
        string commandCorrelationId = record.CorrelationId;
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, commandCorrelationId));
        _ = (activity?.SetTag("eventstore.retry_count", record.RetryCount));
        _ = (activity?.SetTag(EventStoreActivitySource.TagEventCount, record.EventCount));
        _ = (activity?.SetTag("eventstore.drain_start_sequence", record.StartSequence));
        _ = (activity?.SetTag("eventstore.drain_end_sequence", record.EndSequence));

        logger.LogInformation(
            "Drain attempt starting: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}",
            commandCorrelationId,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            record.RetryCount,
            record.EventCount);

        try {
            long expectedEventCount = record.EndSequence - record.StartSequence + 1;
            if (record.EventCount != expectedEventCount) {
                throw new DrainEventCountMismatchException(
                    identity.ActorId,
                    record.StartSequence,
                    record.EndSequence,
                    record.EventCount,
                    expectedEventCount);
            }

            // Load exact persisted event range for this failed command
            IReadOnlyList<EventEnvelope> events;
            try {
                events = await LoadPersistedEventsRangeAsync(
                    identity,
                    record.StartSequence,
                    record.EndSequence)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (IsDrainStateStoreBoundaryFailure(ex)) {
                throw new DrainStateStoreException("Failed to read persisted drain events from state store.", ex);
            }

            // Re-publish events
            EventPublishResult publishResult;
            try {
                publishResult = await eventPublisher
                    .PublishEventsAsync(identity, events, commandCorrelationId)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (IsDrainPublishBoundaryFailure(ex)) {
                throw new DrainPublishException("Drain publish operation failed.", ex);
            }

            if (publishResult.Success) {
                // Success: remove record, decrement backpressure counter, unregister reminder, update advisory status
                await StateManager.RemoveStateAsync(UnpublishedEventsRecord.GetStateKey(trackingId))
                    .ConfigureAwait(false);

                // Story 4.3: Decrement pending command counter on drain success (AC #6)
                _ = await DecrementPendingCommandCountAsync().ConfigureAwait(false);

                await StateManager.SaveStateAsync().ConfigureAwait(false);

                try {
                    await UnregisterReminderAsync(UnpublishedEventsRecord.GetReminderName(trackingId))
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    logger.LogWarning(
                        ex,
                        "Failed to unregister drain reminder after success: CorrelationId={CorrelationId}",
                        commandCorrelationId);
                }

                // Advisory status: Completed or Rejected based on event type
                CommandStatus drainStatus = record.IsRejection ? CommandStatus.Rejected : CommandStatus.Completed;
                try {
                    await commandStatusStore.WriteStatusAsync(
                        identity.TenantId,
                        record.MessageId ?? trackingId,
                        new CommandStatusRecord(
                            drainStatus,
                            DateTimeOffset.UtcNow,
                            identity.AggregateId,
                            EventCount: record.EventCount,
                            RejectionEventType: null,
                            FailureReason: null,
                            TimeoutDuration: null,
                            MessageId: record.MessageId,
                            CorrelationId: commandCorrelationId)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    // Rule #12: Advisory status writes -- failure logged, never thrown.
                    logger.LogWarning(
                        ex,
                        "Advisory status write failed after drain success: CorrelationId={CorrelationId}, Status={Status}",
                        commandCorrelationId,
                        drainStatus);
                }

                logger.LogInformation(
                    "Drain succeeded: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}",
                    commandCorrelationId,
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
                    UnpublishedEventsRecord.GetStateKey(trackingId),
                    updatedRecord).ConfigureAwait(false);
                await StateManager.SaveStateAsync().ConfigureAwait(false);

                logger.LogWarning(
                    "Drain failed: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}, StartSequence={StartSequence}, EndSequence={EndSequence}, FailureReason={FailureReason}",
                    commandCorrelationId,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    updatedRecord.RetryCount,
                    updatedRecord.EventCount,
                    updatedRecord.StartSequence,
                    updatedRecord.EndSequence,
                    publishResult.FailureReason);

                _ = (activity?.SetTag("eventstore.retry_count", updatedRecord.RetryCount));
                _ = (activity?.SetTag("eventstore.failure_reason", DrainReasonCodes.PublishFailed));
                _ = (activity?.SetStatus(ActivityStatusCode.Error, DrainReasonCodes.PublishFailed));
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            // Drain infrastructure failure: increment retry, save, reminder continues
            string failureReasonCode = ClassifyDrainFailure(ex);
            _ = (activity?.SetTag("eventstore.failure_reason", failureReasonCode));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, failureReasonCode));

            string safeFailureReason = ProtectedDataDiagnosticRedactor.RedactException(ex, "drain");
            UnpublishedEventsRecord updatedRecord = record.IncrementRetry(safeFailureReason);
            await StateManager.SetStateAsync(
                UnpublishedEventsRecord.GetStateKey(trackingId),
                updatedRecord).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);

            logger.LogWarning(
                "Drain failed with exception: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, EventCount={EventCount}, StartSequence={StartSequence}, EndSequence={EndSequence}, FailureReason={FailureReason}",
                commandCorrelationId,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                updatedRecord.RetryCount,
                updatedRecord.EventCount,
                updatedRecord.StartSequence,
                updatedRecord.EndSequence,
                safeFailureReason);

            _ = (activity?.SetTag("eventstore.retry_count", updatedRecord.RetryCount));
        }
    }

    internal static string ClassifyDrainFailure(Exception exception) =>
        exception switch {
            DrainPublishException => DrainReasonCodes.PublishFailed,
            DrainStateStoreException => DrainReasonCodes.StateStoreFailure,
            DrainEventCountMismatchException => DrainReasonCodes.EventCountMismatch,
            MissingEventException => DrainReasonCodes.MissingEvent,
            EventDeserializationException => DrainReasonCodes.StateStoreFailure,
            DaprException when ContainsDaprUnavailableSignal(exception) => DrainReasonCodes.DaprUnavailable,
            RpcException rpc when IsUnavailableStatusCode(rpc.StatusCode) => DrainReasonCodes.DaprUnavailable,
            HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests } => DrainReasonCodes.DaprUnavailable,
            DaprException => DrainReasonCodes.StateStoreFailure,
            _ => DrainReasonCodes.Unknown,
        };

    private static bool IsDrainStateStoreBoundaryFailure(Exception exception) =>
        exception is EventDeserializationException
        or DaprException
        or RpcException
        or HttpRequestException;

    private static bool IsDrainPublishBoundaryFailure(Exception exception) =>
        exception is DaprException
        or RpcException
        or HttpRequestException
        or IOException
        or TimeoutException;

    private static bool ContainsDaprUnavailableSignal(Exception exception) {
        for (Exception? current = exception; current is not null; current = current.InnerException) {
            if (current is RpcException rpc && IsUnavailableStatusCode(rpc.StatusCode)) {
                return true;
            }

            if (current is HttpRequestException { StatusCode: HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests }) {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnavailableStatusCode(StatusCode statusCode) =>
        statusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.ResourceExhausted;

    // TODO: Future — reconcile counter against actual drain:* record count on actor activation
    private async Task<int> ReadPendingCommandCountAsync() {
        ConditionalValue<int> result = await StateManager
            .TryGetStateAsync<int>(PendingCommandCountKey)
            .ConfigureAwait(false);
        return result.HasValue ? result.Value : 0;
    }

    private async Task StagePendingCommandCountAsync(int newCount) => await StateManager.SetStateAsync(PendingCommandCountKey, newCount).ConfigureAwait(false);

    private async Task<int> DecrementPendingCommandCountAsync() {
        int current = await ReadPendingCommandCountAsync().ConfigureAwait(false);
        if (current <= 0) {
            logger.LogWarning(
                "Pending command count was already 0 during decrement: ActorId={ActorId}. Possible counter drift.",
                Host.Id);
            return 0;
        }

        int newCount = current - 1;
        await StateManager.SetStateAsync(PendingCommandCountKey, newCount).ConfigureAwait(false);
        return newCount;
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

    private static bool CanRepresentCommittedEvents(PipelineState pipelineState)
    {
        ArgumentNullException.ThrowIfNull(pipelineState);
        return pipelineState.EventCount is > 0
            || pipelineState.CurrentStage is CommandStatus.EventsStored
                or CommandStatus.EventsPublished
                or CommandStatus.Completed
                or CommandStatus.PublishFailed;
    }

    private static bool HasCompletePipelineIdentity(PipelineState pipelineState)
    {
        ArgumentNullException.ThrowIfNull(pipelineState);
        return !string.IsNullOrWhiteSpace(pipelineState.MessageId)
            && !string.IsNullOrWhiteSpace(pipelineState.CausationId)
            && !string.IsNullOrWhiteSpace(pipelineState.CommandType);
    }

    private async Task HandoffStaleCommittedCheckpointAsync(
        PipelineState stalePipeline,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix)
    {
        string staleMessageId = stalePipeline.MessageId
            ?? throw new InvalidOperationException("A stale committed checkpoint requires a message identity.");
        int eventCount = stalePipeline.EventCount ?? 0;

        if (eventCount <= 0)
        {
            await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, stalePipeline.CorrelationId)
                .ConfigureAwait(false);
            _ = await DecrementPendingCommandCountAsync().ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            return;
        }

        // Use the checkpoint's persisted committed range -- NEVER re-derive it from the mutable stream
        // head, which an interleaved different-correlation command may have advanced past this command's
        // events. The caller guards against a legacy checkpoint that lacks the range, so it is required here.
        long startSequence = stalePipeline.StartSequence
            ?? throw new InvalidOperationException("Cannot hand off a stale committed checkpoint without a persisted start sequence.");
        long endSequence = stalePipeline.EndSequence
            ?? throw new InvalidOperationException("Cannot hand off a stale committed checkpoint without a persisted end sequence.");
        if (startSequence < 1 || endSequence < startSequence || (endSequence - startSequence + 1) != eventCount)
        {
            throw new InvalidOperationException("Cannot hand off a stale committed checkpoint with an invalid event range.");
        }

        var unpublishedRecord = new UnpublishedEventsRecord(
            stalePipeline.CorrelationId,
            startSequence,
            endSequence,
            eventCount,
            stalePipeline.CommandType,
            stalePipeline.RejectionEventType is not null,
            DateTimeOffset.UtcNow,
            RetryCount: 0,
            LastFailureReason: "stale_checkpoint_handoff",
            MessageId: staleMessageId);

        await StoreDrainRecordAndRegisterReminderAsync(staleMessageId, unpublishedRecord)
            .ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, stalePipeline.CorrelationId)
            .ConfigureAwait(false);
        await StateManager.SaveStateAsync().ConfigureAwait(false);
        await RegisterDrainReminderAsync(staleMessageId).ConfigureAwait(false);

        Log.StaleCommittedCheckpointHandedOff(
            logger,
            stalePipeline.CorrelationId,
            staleMessageId,
            stalePipeline.CurrentStage.ToString(),
            eventCount);
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
            if (existingPipeline.StartSequence is not long resumeStart
                || existingPipeline.EndSequence is not long resumeEnd) {
                // Legacy committed checkpoint without a persisted event range: the exact events cannot
                // be identified safely (an interleaved command may have advanced the stream head), so
                // fail closed rather than re-publishing a guessed range. The checkpoint is preserved.
                Log.PipelineIdentityConflict(
                    logger,
                    command.CorrelationId,
                    command.MessageId,
                    existingPipeline.CurrentStage.ToString());
                _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "CommandIdentityConflict"));
                return new CommandProcessingResult(
                    Accepted: false,
                    ErrorMessage: "command_identity_conflict",
                    CorrelationId: command.CorrelationId);
            }

            IReadOnlyList<EventEnvelope> persistedEvents;
            try {
                persistedEvents = await LoadPersistedEventsRangeAsync(command.AggregateIdentity, resumeStart, resumeEnd)
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
                .PublishEventsAsync(
                    command.AggregateIdentity,
                    persistedEvents,
                    command.CorrelationId,
                    triggerProjectionUpdate: false)
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
                RejectionEventType: existingPipeline.RejectionEventType,
                MessageId: existingPipeline.MessageId,
                CausationId: existingPipeline.CausationId,
                StartSequence: existingPipeline.StartSequence,
                EndSequence: existingPipeline.EndSequence);

            await stateMachine.CheckpointAsync(pipelineKeyPrefix, eventsPublishedState).ConfigureAwait(false);

            LogStageTransition(CommandStatus.EventsPublished, command, causationId, startTicks);
            await WriteAdvisoryStatusAsync(command, CommandStatus.EventsPublished).ConfigureAwait(false);
        }

        bool accepted = existingPipeline.RejectionEventType is null;
        string? errorMessage = existingPipeline.RejectionEventType is not null
            ? $"Domain rejection: {existingPipeline.RejectionEventType}"
            : null;

        _ = await DecrementPendingCommandCountAsync().ConfigureAwait(false);

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
            startTicks,
            rejectionEventType: existingPipeline.RejectionEventType).ConfigureAwait(false);

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
            RejectionEventType: existingPipeline.RejectionEventType,
            MessageId: existingPipeline.MessageId,
            CausationId: existingPipeline.CausationId,
            StartSequence: existingPipeline.StartSequence,
            EndSequence: existingPipeline.EndSequence);
        await stateMachine.CheckpointAsync(pipelineKeyPrefix, publishFailedState).ConfigureAwait(false);

        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        CommandProcessingResult failResult = CreatePublishFailedResult(
            command.CorrelationId,
            existingPipeline.EventCount ?? 0,
            failureReason,
            existingPipeline.RejectionEventType);

        await RecordIdempotencyAsync(
            idempotencyChecker,
            CreateCommandProcessingIdentity(command),
            failResult,
            IdempotencyRecordDisposition.Recoverable).ConfigureAwait(false);

        // Story 4.2: Store drain record for recovery on resume path (committed in same atomic batch)
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
            else if (existingPipeline.StartSequence is long checkpointStart
                && existingPipeline.EndSequence is long checkpointEnd) {
                // Use the checkpoint's persisted committed range -- never re-derive from the mutable
                // stream head, which an interleaved command may have advanced past this command's events.
                startSequence = checkpointStart;
                endSequence = checkpointEnd;
                hasRange = true;
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
                    LastFailureReason: failureReason,
                    MessageId: existingPipeline.MessageId ?? command.MessageId);
                string drainTrackingId = existingPipeline.MessageId ?? command.MessageId;
                await StoreDrainRecordAndRegisterReminderAsync(drainTrackingId, unpublishedRecord)
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
                innerException: ex,
                messageId: command.MessageId);
        }

        // Story 4.2: Register drain reminder AFTER successful commit
        if (shouldRegisterReminder) {
            await RegisterDrainReminderAsync(existingPipeline.MessageId ?? command.MessageId).ConfigureAwait(false);
        }

        LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);
        await WriteAdvisoryStatusAsync(
            command,
            CommandStatus.PublishFailed,
            failureReason,
            existingPipeline.EventCount,
            existingPipeline.RejectionEventType).ConfigureAwait(false);
        LogCommandCompletedSummary(command, causationId, CommandStatus.PublishFailed, startTicks);

        _ = (processActivity?.SetTag("eventstore.publish_failed", true));
        _ = (processActivity?.SetTag("eventstore.drain_scheduled", true));
        _ = (processActivity?.SetStatus(
            failResult.Accepted ? ActivityStatusCode.Ok : ActivityStatusCode.Error,
            failResult.Accepted ? null : "PublishFailed"));
        return failResult;
    }

    private static CommandProcessingResult CreatePublishFailedResult(
        string correlationId,
        int eventCount,
        string? failureReason,
        string? rejectionEventType,
        string? resultPayload = null) {
        bool accepted = string.IsNullOrWhiteSpace(rejectionEventType);
        string? errorMessage = accepted
            ? null
            : $"Domain rejection: {rejectionEventType}";

        return new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: correlationId,
            EventCount: eventCount,
            ResultPayload: resultPayload);
    }

    private async Task<EventEnvelope[]> ReadEventsRangeAsync(
        AggregateIdentity identity,
        int startSequence,
        int count) {
        if (count <= 0) {
            return [];
        }

        var events = new List<EventEnvelope>(count);
        int endExclusive = startSequence + count;

        for (int seq = startSequence; seq < endExclusive; seq++) {
            ConditionalValue<EventEnvelope> result = await StateManager
                .TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}{seq}")
                .ConfigureAwait(false);

            if (!result.HasValue) {
                throw new MissingEventException(seq, identity.TenantId, identity.Domain, identity.AggregateId);
            }

            events.Add(result.Value);
        }

        return [.. events];
    }

    /// <summary>
    /// Story 22.7b: pre-domain readability boundary. Walks every rehydrated envelope, calls the
    /// metadata-aware typed unprotect entry point, and returns a list of envelopes whose payload
    /// bytes are safe to forward to a domain service (or any caller that needs plaintext). Throws
    /// <see cref="ProtectedDataUnreadableException"/> with a safe reason code (no provider
    /// exception text, no payload bytes, no key alias) when any event is provider-opaque or the
    /// provider classifies it as unreadable.
    /// </summary>
    private async Task<IReadOnlyList<EventEnvelope>> EnsureEventsReadableForDomainAsync(
        AggregateIdentity identity,
        IReadOnlyList<EventEnvelope> events,
        CancellationToken cancellationToken) {
        if (events.Count == 0) {
            return events;
        }

        var readable = new List<EventEnvelope>(events.Count);
        foreach (EventEnvelope envelope in events) {
            EventStorePayloadProtectionMetadata storedMetadata = EventStorePayloadProtectionMetadataCarrier
                .Read(envelope.Extensions);

            // Story 22.7c: route every fail-closed rehydrate decision through the canonical
            // ProtectedDataReadabilityDecisionFactory so the actor, publisher, snapshot manager,
            // and stream reader emit decisions with identical shape.
            if (storedMetadata.State == PayloadProtectionState.ProviderOpaque) {
                ProtectedDataReadabilityDecision opaqueDecision = ProtectedDataReadabilityDecisionFactory.FromMetadata(
                    storedMetadata,
                    ProtectedDataDecisionStage.Rehydrate,
                    identity.TenantId,
                    identity.Domain,
                    identity.AggregateId,
                    envelope.SequenceNumber);
                throw new ProtectedDataUnreadableException(
                    opaqueDecision.UnreadableReason!.Value,
                    stage: ProtectedDataReadabilityDecisionStageCodes.From(opaqueDecision.Stage),
                    sequenceNumber: envelope.SequenceNumber);
            }

            PayloadUnprotectionOutcome outcome;
            try {
                outcome = await payloadProtectionService
                    .TryUnprotectEventPayloadAsync(
                        identity,
                        envelope.EventTypeName,
                        envelope.Payload,
                        envelope.SerializationFormat,
                        storedMetadata,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                outcome = PayloadUnprotectionOutcome.Unreadable(
                    UnreadableProtectedDataReason.ProviderUnavailable,
                    storedMetadata);
            }

            ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecisionFactory.FromOutcome(
                outcome,
                ProtectedDataDecisionStage.Rehydrate,
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                envelope.SequenceNumber);
            if (!decision.IsReadable) {
                throw new ProtectedDataUnreadableException(
                    decision.UnreadableReason!.Value,
                    stage: ProtectedDataReadabilityDecisionStageCodes.From(decision.Stage),
                    sequenceNumber: envelope.SequenceNumber);
            }

            readable.Add(envelope with {
                Payload = outcome.PayloadBytes!,
                SerializationFormat = outcome.SerializationFormat!,
            });
        }

        return readable;
    }

    private static ContractEventEnvelope ToContractEventEnvelope(EventEnvelope envelope) =>
        new(
            new ContractEventMetadata(
                envelope.MessageId,
                envelope.AggregateId,
                envelope.AggregateType,
                envelope.TenantId,
                envelope.Domain,
                envelope.SequenceNumber,
                envelope.GlobalPosition,
                envelope.Timestamp,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.UserId,
                envelope.DomainServiceVersion,
                envelope.EventTypeName,
                envelope.MetadataVersion,
                envelope.SerializationFormat),
            envelope.Payload,
            envelope.Extensions is null ? null : new Dictionary<string, string>(envelope.Extensions));

    private async Task<string> ResolveAggregateTypeAsync(CommandEnvelope command, CancellationToken cancellationToken) {
        if (commandAggregateTypeResolver is not null) {
            string? resolved = await commandAggregateTypeResolver
                .ResolveAsync(command, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(resolved)) {
                return resolved.Trim();
            }
        }

        return command.Domain;
    }

    private static CommandProcessingIdentity CreateCommandProcessingIdentity(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandProcessingIdentity(
            command.MessageId,
            command.CausationId ?? command.MessageId,
            command.CommandType);
    }

    private Task RecordIdempotencyAsync(
        IdempotencyChecker idempotencyChecker,
        CommandProcessingIdentity identity,
        CommandProcessingResult result,
        IdempotencyRecordDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(idempotencyChecker);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(result);

        return idempotencyChecker.RecordAsync(
            identity,
            result,
            IdempotencyTimeProvider.GetUtcNow().AddSeconds(IdempotencyRetentionSeconds),
            disposition);
    }

    /// <summary>
    /// Handles infrastructure failures by routing to dead-letter and transitioning to Rejected.
    /// Dead-letter publication is best-effort and non-blocking (AC #7).
    /// Dead-letter publication happens BEFORE SaveStateAsync (task 6.7).
    /// </summary>
    private async Task<CommandProcessingResult> HandleInfrastructureFailureAsync(
        CommandEnvelope command,
        string causationId,
        CommandStatus failureStage,
        Exception exception,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        Activity? processActivity,
        long startTicks,
        int? eventCount) {
        // Discard any events/metadata staged by the failed persistence step before recording the
        // rejection; otherwise the SaveStateAsync below would durably commit those staged events
        // together with the Rejected result, leaving them persisted but never published. Mirrors the
        // concurrency-conflict path, which already clears the cache before staging its outcome.
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
        string safeFailureReason = ProtectedDataDiagnosticRedactor.RedactException(exception, failureStage.ToString());
        Log.InfrastructureFailure(logger, command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, failureStage.ToString(), exception.GetType().Name, safeFailureReason);

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
            RejectionEventType: null,
            ResultPayload: null,
            MessageId: command.MessageId,
            CausationId: causationId);
        await stateMachine.CheckpointAsync(pipelineKeyPrefix, rejectedState).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        var failResult = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: safeFailureReason,
            CorrelationId: command.CorrelationId,
            EventCount: 0);

        await StateManager.SaveStateAsync().ConfigureAwait(false);

        // Advisory status write (non-blocking per rule #12)
        await WriteAdvisoryStatusAsync(command, CommandStatus.Rejected, safeFailureReason).ConfigureAwait(false);

        LogStageTransition(CommandStatus.Rejected, command, causationId, startTicks);
        LogCommandCompletedSummary(command, causationId, CommandStatus.Rejected, startTicks);
        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "InfrastructureFailure"));
        return failResult;
    }

    /// <summary>
    /// Completes terminal state: records idempotency, cleans up pipeline, commits, writes advisory status.
    /// </summary>
    private async Task<CommandProcessingResult> CompleteConcurrencyConflictAsync(
        CommandEnvelope command,
        string causationId,
        ConcurrencyConflictException conflict,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        Activity? processActivity,
        long startTicks,
        int maxPersistenceConflictRetries) {
        await StateManager.ClearCacheAsync().ConfigureAwait(false);

        var result = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: "ConcurrencyConflict",
            CorrelationId: command.CorrelationId,
            EventCount: 0);

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
                innerException: ex,
                messageId: command.MessageId);
        }

        await WriteAdvisoryStatusAsync(
            command,
            CommandStatus.Rejected,
            failureReason: "ConcurrencyConflict").ConfigureAwait(false);

        Log.PersistenceConflictExhausted(
            logger,
            command.CorrelationId,
            causationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            maxPersistenceConflictRetries,
            conflict.ConflictSource ?? "StateStore");

        LogStageTransition(CommandStatus.Rejected, command, causationId, startTicks);
        LogCommandCompletedSummary(command, causationId, CommandStatus.Rejected, startTicks);
        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "ConcurrencyConflict"));
        return result;
    }

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
        long startTicks,
        string? rejectionEventType = null,
        string? resultPayload = null) {
        var result = new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: command.CorrelationId,
            EventCount: eventCount,
            ResultPayload: resultPayload);

        await RecordIdempotencyAsync(
            idempotencyChecker,
            CreateCommandProcessingIdentity(command),
            result,
            IdempotencyRecordDisposition.Terminal).ConfigureAwait(false);
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
                innerException: ex,
                messageId: command.MessageId);
        }

        CommandStatus terminalStatus = accepted ? CommandStatus.Completed : CommandStatus.Rejected;
        LogStageTransition(terminalStatus, command, causationId, startTicks);
        await WriteAdvisoryStatusAsync(
            command, terminalStatus,
            eventCount: eventCount > 0 ? eventCount : null,
            rejectionEventType: rejectionEventType).ConfigureAwait(false);
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
        string? failureReason = null,
        int? eventCount = null,
        string? rejectionEventType = null) {
        try {
            await commandStatusStore.WriteStatusAsync(
                command.TenantId,
                command.MessageId,
                new CommandStatusRecord(
                    status,
                    DateTimeOffset.UtcNow,
                    command.AggregateId,
                    EventCount: eventCount,
                    RejectionEventType: rejectionEventType,
                    FailureReason: failureReason,
                    TimeoutDuration: null,
                    MessageId: command.MessageId,
                    CorrelationId: command.CorrelationId)).ConfigureAwait(false);
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
            EventId = 2005,
            Level = LogLevel.Warning,
            Message = "Backpressure rejected: ActorId={ActorId}, CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, PendingCount={PendingCount}, Threshold={Threshold}, Stage=BackpressureRejected")]
        public static partial void BackpressureRejected(
            ILogger logger,
            string actorId,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            int pendingCount,
            int threshold);

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

        [LoggerMessage(
            EventId = 2006,
            Level = LogLevel.Warning,
            Message = "Persistence conflict retry: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, Attempt={Attempt}, MaxRetries={MaxRetries}, Stage=PersistenceConflictRetry")]
        public static partial void PersistenceConflictRetry(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            int attempt,
            int maxRetries);

        [LoggerMessage(
            EventId = 2007,
            Level = LogLevel.Warning,
            Message = "Persistence conflict retries exhausted: CorrelationId={CorrelationId}, CausationId={CausationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, MaxRetries={MaxRetries}, ConflictSource={ConflictSource}, Stage=PersistenceConflictExhausted")]
        public static partial void PersistenceConflictExhausted(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            int maxRetries,
            string conflictSource);

        [LoggerMessage(
            EventId = 2008,
            Level = LogLevel.Warning,
            Message = "Pipeline identity conflict: CorrelationId={CorrelationId}, IncomingMessageId={IncomingMessageId}, PersistedStage={PersistedStage}, Stage=PipelineIdentityConflict")]
        public static partial void PipelineIdentityConflict(
            ILogger logger,
            string correlationId,
            string incomingMessageId,
            string persistedStage);

        [LoggerMessage(
            EventId = 2009,
            Level = LogLevel.Warning,
            Message = "Stale committed checkpoint handed to drain recovery: CorrelationId={CorrelationId}, MessageId={MessageId}, PersistedStage={PersistedStage}, EventCount={EventCount}, Stage=StaleCheckpointHandoff")]
        public static partial void StaleCommittedCheckpointHandedOff(
            ILogger logger,
            string correlationId,
            string messageId,
            string persistedStage,
            int eventCount);
    }
}
