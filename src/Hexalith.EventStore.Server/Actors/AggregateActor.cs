
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
    TimeProvider? timeProvider = null,
    IdempotencyExecutionContextProtector? executionContextProtector = null)
    : Actor(host), IAggregateActor, IIdempotencyLegacySourceActor, IRemindable {
    private const string TraceParentExtensionKey = "traceparent";
    private const string TraceStateExtensionKey = "tracestate";
    private const string PendingCommandCountKey = "pending_command_count";
    private int _pendingFinalizerCommittedBefore = -1;
    private int _pendingFinalizerExpectedAfter = -1;
    private bool _pendingFinalizerRecoveryRequired;
    private bool _pendingCountReconciliationRequired;
    private bool _stateCacheUnsafe;

    /// <summary>
    /// Story 4.4: the maximum number of index entries one activation re-arms. Activation must stay
    /// cheap (see <c>ITenantValidatorActor</c>: "avoid expensive I/O in OnActivateAsync"), so the
    /// remainder is carried to the next activation rather than blocking this one on many saves plus
    /// reminder registrations.
    /// </summary>
    private const int MaxActivationRearmEntries = 8;

    /// <summary>
    /// Story 4.4: the maximum number of index entries one activation may take past the
    /// already-armed skip into a state-backed recovery path (unarmed drain re-arm or checkpoint
    /// rebuild). Entries whose reminder is already confirmed armed are skipped without charging
    /// this budget — otherwise a hot head of armed entries permanently starves every unarmed
    /// entry past this bound. The scan still starts at the head of the index (oldest first).
    /// </summary>
    private const int MaxActivationProbeEntries = 32;

    private int MaxPersistenceConflictRetries
        => Math.Max(
            0,
            concurrencyOptions?.Value.MaxPersistenceConflictRetries
            ?? CommandConcurrencyOptions.DefaultMaxPersistenceConflictRetries);

    private int IdempotencyRetentionSeconds
        => idempotencyRetentionOptions?.Value.TerminalRetentionSeconds
            ?? IdempotencyRetentionOptions.DefaultTerminalRetentionSeconds;

    private TimeProvider IdempotencyTimeProvider { get; } = timeProvider ?? TimeProvider.System;

    /// <summary>Gets the normalized bounded drain-attempt budget for one committed range.</summary>
    private int MaxDrainAttempts
        => EventDrainOptions.NormalizeMaxDrainAttempts(drainOptions.Value.MaxDrainAttempts);

    /// <summary>Gets the normalized bound on outstanding publication-recovery index entries.</summary>
    private int MaxOutstandingPublicationEntries
        => EventDrainOptions.NormalizeMaxOutstandingPublicationEntries(
            drainOptions.Value.MaxOutstandingPublicationEntries,
            backpressureOptions.Value.MaxPendingCommandsPerAggregate);

    /// <summary>
    /// Story 4.4: re-arms every committed-but-unpublished command that lost its drain record or its
    /// reminder to a crash in the window between the event commit and the reminder registration.
    /// <para>
    /// Mirrors <see cref="ETagActor"/>: the whole body is wrapped and degrades to a no-op on any
    /// failure, because a throwing activation bricks the aggregate. Activation only RE-ARMS -- it
    /// never publishes and never calls another actor (reentrancy is disabled repo-wide, so an
    /// activation-time actor call would deadlock). The reminder does the publishing.
    /// </para>
    /// </summary>
    protected override async Task OnActivateAsync() {
        _pendingCountReconciliationRequired = true;
        try {
            UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
            if (index.Entries.Count > 0) {
                await RearmOutstandingPublicationsAsync(index).ConfigureAwait(false);
            }

            await ReconcilePendingCommandCountAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            // Degrade, never fail activation. The entries stay durable for the next activation.
            _pendingCountReconciliationRequired = true;
            Log.PublicationRecoveryDegraded(logger, Host.Id.GetId(), ex.GetType().Name);
        }
    }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessFencedCommandAsync(FencedCommandEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentNullException.ThrowIfNull(request.ExecutionContext);
        return ProcessCommandCoreAsync(request.Command, request.ExecutionContext, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(FencedCommandEnvelope request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Command);
        ArgumentNullException.ThrowIfNull(request.ExecutionContext);
        IdempotencyExecutionContextProtector protector = executionContextProtector
            ?? throw new InvalidOperationException("Idempotency execution-fence validation is unavailable.");
        await protector.ValidateReconciliationAsync(
            request.ExecutionContext,
            request.Command,
            CancellationToken.None).ConfigureAwait(false);
        var tenantValidator = new TenantValidator(Host.LoggerFactory.CreateLogger<TenantValidator>());
        tenantValidator.Validate(request.Command.TenantId, Host.Id.GetId());
        await EnsureStateCacheBarrierAsync(
            request.Command.CorrelationId,
            activity: null).ConfigureAwait(false);
        var checker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>(),
            IdempotencyTimeProvider);
        return await checker
            .InspectAsync(CreateCommandProcessingIdentity(request.Command))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    async Task<IdempotencyLegacySourceInspection> IIdempotencyLegacySourceActor.InspectLegacySourceAsync(
        IdempotencyLegacySourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantValidator = new TenantValidator(Host.LoggerFactory.CreateLogger<TenantValidator>());
        tenantValidator.Validate(request.TenantPartition, Host.Id.GetId());
        await EnsureStateCacheBarrierAsync(
            request.ExecutionCorrelationId,
            activity: null).ConfigureAwait(false);
        var checker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>(),
            IdempotencyTimeProvider);
        return await checker.InspectLegacySourceAsync(request).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    async Task<IdempotencyLegacySourceInspection> IIdempotencyLegacySourceActor.SetLegacySourceRedirectAsync(
        IdempotencyLegacySourceRedirectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);
        var tenantValidator = new TenantValidator(Host.LoggerFactory.CreateLogger<TenantValidator>());
        tenantValidator.Validate(request.Source.TenantPartition, Host.Id.GetId());
        await EnsureStateCacheBarrierAsync(
            request.Source.ExecutionCorrelationId,
            activity: null).ConfigureAwait(false);
        var checker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>(),
            IdempotencyTimeProvider);
        try
        {
            return await checker.SetLegacySourceRedirectAsync(request).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
                .ConfigureAwait(false);
            if (!discarded)
            {
                Log.ActorStateRemediationFailed(
                    logger,
                    Host.Id.GetId(),
                    request.Source.ExecutionCorrelationId,
                    "LegacyRedirect",
                    exception.GetType().Name,
                    "DiscardRedirectBatch",
                    exception.GetType().Name,
                    discardExceptionType,
                    failedBatchDiscarded: false,
                    durableStateObservation: "Unobserved");
                return new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unavailable);
            }

            IdempotencyLegacySourceInspection observed = await checker
                .InspectLegacySourceAsync(request.Source)
                .ConfigureAwait(false);
            return observed.Decision == IdempotencyLegacySourceDecision.Redirected
                ? observed
                : new IdempotencyLegacySourceInspection(IdempotencyLegacySourceDecision.Unavailable);
        }
    }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ProcessCommandCoreAsync(command, executionContext: null, CancellationToken.None);
    }

    /// <summary>
    /// Processes a command envelope within the aggregate actor context.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="cancellationToken">Cancellation token for local/in-process callers.</param>
    /// <returns>The result of processing the command.</returns>
    public Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ProcessCommandCoreAsync(command, executionContext: null, cancellationToken);
    }

    private async Task<CommandProcessingResult> ProcessCommandCoreAsync(
        CommandEnvelope command,
        IdempotencyExecutionContext? executionContext,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);

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
            int pendingCommandCountBeforeAdmission = -1;

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

            await EnsureStateCacheBarrierAsync(command.CorrelationId, processActivity).ConfigureAwait(false);

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
                    try
                    {
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                    }
                    catch (Exception migrationSaveException)
                    {
                        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
                            .ConfigureAwait(false);
                        if (!discarded)
                        {
                            throw CreateStateRemediationException(
                                command.CorrelationId,
                                "LegacyMigrationSave",
                                migrationSaveException,
                                "DiscardLegacyMigrationBatch",
                                migrationSaveException,
                                discardExceptionType,
                                failedBatchDiscarded: false,
                                durableStateObservation: "Unobserved");
                        }

                        IdempotencyCheckResult observed;
                        try
                        {
                            observed = await idempotencyChecker
                                .InspectAsync(commandIdentity)
                                .ConfigureAwait(false);
                        }
                        catch (Exception inspectionException)
                        {
                            _stateCacheUnsafe = true;
                            throw CreateStateRemediationException(
                                command.CorrelationId,
                                "LegacyMigrationSave",
                                migrationSaveException,
                                "InspectLegacyMigrationCommit",
                                inspectionException,
                                discardExceptionType,
                                failedBatchDiscarded: true,
                                durableStateObservation: "DurableInspectionFailed");
                        }

                        if (observed.Outcome is IdempotencyCheckOutcome.ExactTerminalDuplicate
                            or IdempotencyCheckOutcome.RetryableRecoverable)
                        {
                            idempotencyCheck = observed;
                        }
                        else
                        {
                            throw;
                        }
                    }
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

                if (idempotencyCheck.Outcome == IdempotencyCheckOutcome.RedirectedLegacy)
                {
                    _ = (activity?.SetStatus(ActivityStatusCode.Error, "LegacyAuthorityRedirected"));
                    _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "LegacyAuthorityRedirected"));
                    return new CommandProcessingResult(
                        Accepted: false,
                        ErrorMessage: "idempotency_legacy_redirected",
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
                        catch (OperationCanceledException)
                        {
                            _ = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
                            throw;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // A handoff that cannot complete must not fault the actor turn, which DAPR
                            // would redeliver into the same fault (poison loop). Discard any uncommitted
                            // staged state, preserve the checkpoint, and fail closed on the incoming command.
                            (bool discarded, string discardExceptionType) =
                                await TryDiscardFailedBatchAsync().ConfigureAwait(false);
                            if (!discarded)
                            {
                                throw CreateStateRemediationException(
                                    command.CorrelationId,
                                    "StaleCheckpointHandoff",
                                    ex,
                                    "DiscardStaleHandoffFailure",
                                    ex,
                                    discardExceptionType,
                                    failedBatchDiscarded: false,
                                    durableStateObservation: "Unobserved");
                            }

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
                        try
                        {
                            await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                                .ConfigureAwait(false);
                            await StateManager.SaveStateAsync().ConfigureAwait(false);
                        }
                        catch (Exception cleanupSaveException)
                        {
                            bool cleanupCommitted = await InspectPipelineCleanupSaveFailureAsync(
                                command.CorrelationId,
                                $"{pipelineKeyPrefix}{command.CorrelationId}",
                                "ProcessingCheckpointCleanup",
                                existingPipeline,
                                cleanupSaveException).ConfigureAwait(false);
                            if (!cleanupCommitted)
                            {
                                throw;
                            }
                        }

                        // A crashed Processing checkpoint may still own one pending slot, but an
                        // activation reconciliation may already have reduced the idle projection to
                        // publication owners only. Reuse only a proven excess slot; otherwise let
                        // normal admission acquire a new one without consuming another owner's slot.
                        try
                        {
                            UnpublishedPublicationIndex publicationOwners = await ReadPublicationIndexAsync()
                                .ConfigureAwait(false);
                            pendingCommandCountBeforeAdmission = await ReadPendingCommandCountAsync()
                                .ConfigureAwait(false);
                            pendingCommandTracked = pendingCommandCountBeforeAdmission > publicationOwners.OwnerCount;
                        }
                        catch (Exception countReadException) when (countReadException is not OperationCanceledException)
                        {
                            _pendingCountReconciliationRequired = true;
                            return await HandleInfrastructureFailureAsync(
                                command,
                                causationId,
                                CommandStatus.Processing,
                                countReadException,
                                stateMachine,
                                pipelineKeyPrefix,
                                processActivity,
                                startTicks,
                                eventCount: null,
                                cancellationToken).ConfigureAwait(false);
                        }
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
                        pendingCommandCountBeforeAdmission = pendingCount;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) {
                        // A guessed zero must never be persisted over slots owned by committed
                        // publication-recovery entries. Route through the existing bounded
                        // infrastructure failure path, which first proves the cache clean.
                        logger.LogWarning(
                            ex,
                            "Backpressure check state read failed: ActorId={ActorId}, CorrelationId={CorrelationId}. Command admission stopped.",
                            Host.Id,
                            command.CorrelationId);
                        return await HandleInfrastructureFailureAsync(
                            command,
                            causationId,
                            CommandStatus.Processing,
                            ex,
                            stateMachine,
                            pipelineKeyPrefix,
                            processActivity,
                            startTicks,
                            eventCount: null,
                            cancellationToken).ConfigureAwait(false);
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
                        BackpressureThreshold: bpOptions.MaxPendingCommandsPerAggregate,
                        FailureReason: "BackpressureExceeded");
                    }

                    await StagePendingCommandCountAsync(pendingCount + 1).ConfigureAwait(false);
                    pendingCommandCountBeforeAdmission = pendingCount;

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
                try
                {
                    await StateManager.SaveStateAsync().ConfigureAwait(false);
                    pendingCommandTracked = true;
                }
                catch (Exception admissionSaveException)
                {
                    pendingCommandTracked = await InspectProcessingAdmissionSaveFailureAsync(
                        command,
                        pipelineState,
                        pipelineKeyPrefix,
                        pendingCommandCountBeforeAdmission,
                        pendingCommandTracked
                            ? pendingCommandCountBeforeAdmission
                            : checked(pendingCommandCountBeforeAdmission + 1),
                        admissionSaveException).ConfigureAwait(false);
                    if (!pendingCommandTracked)
                    {
                        throw;
                    }
                }

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
                            processActivity, startTicks, eventCount: null, cancellationToken).ConfigureAwait(false);
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
                        await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
                        domainResult = await domainServiceInvoker
                            .InvokeAsync(command, currentState, cancellationToken)
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
                            processActivity, startTicks, eventCount: null, cancellationToken).ConfigureAwait(false);
                    }
                }

                string domainServiceVersion = DaprDomainServiceInvoker.ExtractVersion(command, logger);

                // Handle no-op path (AC #12): Processing -> Completed directly
                if (domainResult.IsNoOp) {
                    await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
                    return await CompleteTerminalAsync(
                        command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        accepted: true, eventCount: 0, errorMessage: null,
                        expectedPreCommitPipeline: pipelineState,
                        processActivity, startTicks).ConfigureAwait(false);
                }

                // Step 5: Event persistence (Story 3.7)
                // Dead-letter routing handles infrastructure exceptions.
                EventPersistResult persistResult;
                PipelineState? eventsStoredState = null;
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

                        await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
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
                                await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
                                long preEventSequence = persistResult.NewSequenceNumber - domainResult.Events.Count;
                                await snapshotManager
                                    .CreateSnapshotAsync(command.AggregateIdentity, preEventSequence, currentState, StateManager, command.CorrelationId)
                                    .ConfigureAwait(false);
                            }
                        }

                        // Story 4.4: stage the publication-recovery entry into the SAME batch that
                        // commits the events, so it becomes durable at exactly the instant they do.
                        // FAIL CLOSED on any refusal: committing a range with no recovery entry
                        // recreates the exact crash window this story exists to remove.
                        PublicationIndexAddOutcome indexOutcome =
                            await TryStagePublicationIndexEntryAsync(command.MessageId, command.CorrelationId)
                                .ConfigureAwait(false);
                        if (indexOutcome != PublicationIndexAddOutcome.Added) {
                            _ = (activity?.SetStatus(
                                ActivityStatusCode.Error,
                                indexOutcome == PublicationIndexAddOutcome.AtCapacity
                                    ? "BackpressureExceeded"
                                    : "PublicationIndexEntryInvalid"));
                            return await RejectPublicationIndexRefusalAsync(
                                command,
                                causationId,
                                indexOutcome,
                                pipelineState,
                                stateMachine,
                                pipelineKeyPrefix,
                                processActivity,
                                startTicks).ConfigureAwait(false);
                        }

                        // Checkpoint EventsStored in SAME batch as events (AC #9)
                        string? rejectionEventType = domainResult.IsRejection
                            ? GetEventTypeName(domainResult.Events[0])
                            : null;
                        eventsStoredState = new PipelineState(
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
                            await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
                            await StateManager.SaveStateAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            ConcurrencyConflictException? conflict = ex is InvalidOperationException
                                ? new ConcurrencyConflictException(
                                    command.CorrelationId,
                                    command.AggregateId,
                                    command.TenantId,
                                    conflictSource: "StateStore",
                                    innerException: ex,
                                    messageId: command.MessageId)
                                : null;

                            bool eventBatchCommitted = await InspectEventBatchSaveFailureAsync(
                                command,
                                pipelineState,
                                eventsStoredState,
                                persistResult,
                                conflict ?? ex,
                                conflict is null ? CommandStatus.EventsStored.ToString() : "PersistenceConflict",
                                cancellationToken).ConfigureAwait(false);
                            if (!eventBatchCommitted && conflict is null)
                            {
                                throw;
                            }

                            if (!eventBatchCommitted
                                && persistenceConflictRetryCount < maxPersistenceConflictRetries) {
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

                                try
                                {
                                    await StateManager.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
                                    _stateCacheUnsafe = false;
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                                {
                                    (bool discarded, string discardExceptionType) =
                                        await TryDiscardFailedBatchAsync().ConfigureAwait(false);
                                    if (!discarded)
                                    {
                                        Log.ActorStateRemediationFailed(
                                            logger,
                                            Host.Id.GetId(),
                                            command.CorrelationId,
                                            "PersistenceConflict",
                                            conflict!.GetType().Name,
                                            "ClearCacheBeforeRetryCancellation",
                                            nameof(OperationCanceledException),
                                            discardExceptionType,
                                            failedBatchDiscarded: false,
                                            durableStateObservation: "Unobserved");
                                    }

                                    throw;
                                }
                                catch (Exception remediationException)
                                {
                                    throw await CreateRemediationExceptionAsync(
                                        command.CorrelationId,
                                        "PersistenceConflict",
                                        conflict!,
                                        "ClearCacheBeforeRetry",
                                        remediationException,
                                        attemptDiscard: true).ConfigureAwait(false);
                                }

                                goto RetryAfterPersistenceConflict;
                            }

                            if (!eventBatchCommitted)
                            {
                                return await CompleteConcurrencyConflictAsync(
                                    command,
                                    causationId,
                                    conflict!,
                                    stateMachine,
                                    pipelineKeyPrefix,
                                    processActivity,
                                    startTicks,
                                    maxPersistenceConflictRetries).ConfigureAwait(false);
                            }
                        }

                        LogStageTransition(CommandStatus.EventsStored, command, causationId, startTicks);
                        await WriteAdvisoryStatusAsync(command, CommandStatus.EventsStored).ConfigureAwait(false);

                        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    }
                    catch (ActorStateRemediationException ex) {
                        ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "event-persist");
                        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "ActorStateRemediationFailed"));
                        throw;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException and not ConcurrencyConflictException) {
                        ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "event-persist");
                        // Event persistence infrastructure failure -- dead-letter routing
                        try
                        {
                            return await HandleInfrastructureFailureAsync(
                                command, causationId, CommandStatus.EventsStored, ex,
                                stateMachine, pipelineKeyPrefix,
                                processActivity, startTicks, eventCount: domainResult.Events.Count, cancellationToken).ConfigureAwait(false);
                        }
                        catch (ActorStateRemediationException remediationException)
                        {
                            ProtectedDataDiagnosticRedactor.RecordActivityException(
                                activity,
                                remediationException,
                                "event-persist");
                            _ = (processActivity?.SetStatus(
                                ActivityStatusCode.Error,
                                "ActorStateRemediationFailed"));
                            throw;
                        }
                    }
                }

                // Story 4.1: Publish events via DAPR pub/sub with CloudEvents 1.0
                // Rejection events ARE published (D3: rejection events are normal events).
                await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
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

                    await EnsureExecutionFenceAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
                    return await CompleteTerminalAsync(
                        command, causationId, idempotencyChecker, stateMachine, pipelineKeyPrefix,
                        accepted, domainResult.Events.Count, errorMessage,
                        eventsStoredState
                            ?? throw new InvalidOperationException("EventsStored checkpoint was not established."),
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
                        IdempotencyTimeProvider.GetUtcNow(),
                        RetryCount: 0,
                        LastFailureReason: publishResult.FailureReason,
                        MessageId: command.MessageId);
                    bool recoveryEntryTracked = await StoreDrainRecordAndRegisterReminderAsync(
                        command.MessageId,
                        unpublishedRecord).ConfigureAwait(false);

                    try {
                        await StateManager.SaveStateAsync().ConfigureAwait(false);
                        drainRecordCreated = true;
                    }
                    catch (Exception ex) {
                        (bool recoveryBatchCommitted, bool recoveryOwnerCommitted) =
                            await InspectPublicationRecoverySaveFailureAsync(
                                command,
                                unpublishedRecord,
                                failResult,
                                idempotencyChecker,
                                pipelineKeyPrefix,
                                ex).ConfigureAwait(false);
                        drainRecordCreated = recoveryOwnerCommitted;
                        if (!recoveryBatchCommitted)
                        {
                            if (ex is InvalidOperationException)
                            {
                                throw new ConcurrencyConflictException(
                                    command.CorrelationId,
                                    command.AggregateId,
                                    command.TenantId,
                                    conflictSource: "StateStore",
                                    innerException: ex,
                                    messageId: command.MessageId);
                            }

                            throw;
                        }
                    }

                    // Story 4.2: Register drain reminder AFTER successful commit
                    // Story 4.4: and stamp the record so the next activation does not reset its
                    // schedule or spend a re-arm slot re-registering a reminder that is already live.
                    bool drainReminderArmed = await ArmDrainReminderAsync(
                        command.MessageId,
                        unpublishedRecord).ConfigureAwait(false);

                    LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);

                    // Story 4.4 (AC3): the poll endpoint must say whether an automatic retry is
                    // actually coming. Leaving Retryable null here would report "legacy record" to a
                    // client polling immediately after a drain was armed. A tracked recovery entry
                    // is equally sufficient: when registration failed, activation re-arms from it,
                    // so reporting false would tell the client to abandon a command the platform is
                    // still going to publish.
                    await WriteAdvisoryStatusAsync(
                        command,
                        CommandStatus.PublishFailed,
                        publishResult.FailureReason,
                        domainResult.Events.Count,
                        rejectionEventType,
                        retryable: drainReminderArmed || recoveryEntryTracked,
                        recoveryReasonCode: DrainReasonCodes.PublishFailed,
                        drainAttemptCount: 0).ConfigureAwait(false);

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
                    await FinalizePendingCommandAsync(command, processActivity).ConfigureAwait(false);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<EventEnvelope[]> GetEventsAsync(long fromSequence) {
        fromSequence = Math.Max(0, fromSequence);
        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);

        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        ConditionalValue<AggregateMetadata> metadataResult;
        try {
            metadataResult = await StateManager
                .TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
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

        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);

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
        await EnsureStateCacheBarrierAsync(correlationId: null, activity: null).ConfigureAwait(false);
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
            await EnsureStateCacheBarrierAsync(correlationId, activity: null).ConfigureAwait(false);
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
            _ = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
            throw;
        }
        catch (ProtectedDataUnreadableException ex) {
            _ = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
            return new ManualSnapshotResult(
                ManualSnapshotOutcome.UnreadableProtected,
                ex.SequenceNumber ?? currentSequence,
                identity.SnapshotKey,
                ex.ReasonCode,
                "Protected event data cannot be safely read.");
        }
        catch (Exception ex) {
            (bool discarded, _) = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
            if (discarded && currentSequence > 0)
            {
                try
                {
                    ConditionalValue<SnapshotRecord> observed = await StateManager
                        .TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey)
                        .ConfigureAwait(false);
                    if (observed.HasValue && observed.Value.SequenceNumber == currentSequence)
                    {
                        return new ManualSnapshotResult(
                            ManualSnapshotOutcome.Created,
                            currentSequence,
                            identity.SnapshotKey,
                            null,
                            null);
                    }
                }
                catch (Exception)
                {
                    _stateCacheUnsafe = true;
                }
            }

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
        await EnsureStateCacheBarrierAsync(trackingId, activity: null).ConfigureAwait(false);
        await DrainUnpublishedEventsAsync(trackingId).ConfigureAwait(false);
    }

    private async Task DrainUnpublishedEventsAsync(string trackingId) {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();

        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.EventsDrain);
        // Story 4.4: the reminder suffix is a tracking id -- a message id for records created since
        // Story 4.2, but a correlation id for legacy records. It gets its own tag; the
        // eventstore.message_id tag is only set once the record proves a real message identity.
        _ = (activity?.SetTag("eventstore.drain_tracking_id", trackingId));
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
        if (!string.IsNullOrWhiteSpace(record.MessageId)) {
            _ = (activity?.SetTag("eventstore.message_id", record.MessageId));
        }

        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, commandCorrelationId));
        _ = (activity?.SetTag("eventstore.retry_count", record.RetryCount));
        _ = (activity?.SetTag(EventStoreActivitySource.TagEventCount, record.EventCount));
        _ = (activity?.SetTag("eventstore.drain_start_sequence", record.StartSequence));
        _ = (activity?.SetTag("eventstore.drain_end_sequence", record.EndSequence));

        // Story 4.4: the bound is checked BEFORE publication so it is a real bound -- an
        // unpublishable range can never consume another attempt once its budget is spent.
        int maxDrainAttempts = MaxDrainAttempts;
        _ = (activity?.SetTag("eventstore.max_drain_attempts", maxDrainAttempts));
        if (record.RetryCount >= maxDrainAttempts) {
            await CompleteDrainExhaustionAsync(identity, trackingId, record, maxDrainAttempts, activity)
                .ConfigureAwait(false);
            return;
        }

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

                // Story 4.4: release the recovery entry and end the recoverable idempotency
                // disposition in the SAME batch. Without the Recoverable -> Terminal transition the
                // record never expires and every later retry of this message id returns
                // RetryableRecoverable forever, even though the events are now published.
                string drainedIdentity = record.GetTrackingIdentity(trackingId);
                UnpublishedPublicationIndex remainingOwners =
                    await StagePublicationIndexRemovalAsync(drainedIdentity).ConfigureAwait(false);
                _ = await CompleteRecoverableIdempotencyAsync(drainedIdentity).ConfigureAwait(false);

                // The counter is an ownership projection of the normalized publication index.
                // Recompute it from the staged post-removal index instead of decrementing a value
                // that might belong to another recovery owner.
                await StagePendingCommandCountAsync(remainingOwners.OwnerCount).ConfigureAwait(false);

                try
                {
                    await StateManager.SaveStateAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupSaveException)
                {
                    bool cleanupCommitted = await InspectDrainCleanupSaveFailureAsync(
                        trackingId,
                        record,
                        cleanupSaveException).ConfigureAwait(false);
                    if (!cleanupCommitted)
                    {
                        throw;
                    }
                }

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
                            CorrelationId: commandCorrelationId,
                            // Story 4.4: the drain completed, so no further automatic attempt follows.
                            Retryable: false,
                            RecoveryReasonCode: null,
                            DrainAttemptCount: record.RetryCount)).ConfigureAwait(false);
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
                updatedRecord = await PersistDrainRetryAsync(trackingId, record, updatedRecord)
                    .ConfigureAwait(false);

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

                // Story 4.4: retryable only while the budget is genuinely left. When THIS attempt
                // raised the count to the cap, the next firing does nothing but dead-letter, so the
                // status must already say so rather than promise one more try.
                bool retryRemains = updatedRecord.RetryCount < maxDrainAttempts;
                await WriteDrainAdvisoryStatusAsync(
                    identity,
                    updatedRecord,
                    updatedRecord.GetTrackingIdentity(trackingId),
                    CommandStatus.PublishFailed,
                    DrainReasonCodes.PublishFailed,
                    retryable: retryRemains,
                    recoveryReasonCode: retryRemains
                        ? DrainReasonCodes.PublishFailed
                        : DrainReasonCodes.AttemptsExhausted,
                    attemptCount: updatedRecord.RetryCount).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (ActorStateRemediationException) {
            throw;
        }
        catch (Exception ex) {
            // Drain infrastructure failure: increment retry, save, reminder continues
            string failureReasonCode = ClassifyDrainFailure(ex);
            _ = (activity?.SetTag("eventstore.failure_reason", failureReasonCode));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, failureReasonCode));

            string safeFailureReason = ProtectedDataDiagnosticRedactor.RedactException(ex, "drain");
            UnpublishedEventsRecord updatedRecord = record.IncrementRetry(safeFailureReason);
            updatedRecord = await PersistDrainRetryAsync(trackingId, record, updatedRecord)
                .ConfigureAwait(false);

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

            // Story 4.4: retryable only while the budget is genuinely left (see the sibling branch).
            bool retryRemains = updatedRecord.RetryCount < maxDrainAttempts;
            await WriteDrainAdvisoryStatusAsync(
                identity,
                updatedRecord,
                updatedRecord.GetTrackingIdentity(trackingId),
                CommandStatus.PublishFailed,
                failureReasonCode,
                retryable: retryRemains,
                recoveryReasonCode: retryRemains
                    ? failureReasonCode
                    : DrainReasonCodes.AttemptsExhausted,
                attemptCount: updatedRecord.RetryCount).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Story 4.4: closes out a committed range whose bounded drain budget is spent.
    /// Ordering is load-bearing: dead-letter first, durably mark the record as dead-lettered, and
    /// only then perform the post-publish mutations. A fault between the mark and the mutations
    /// replays only the mutations, so the same range can never be dead-lettered twice; a fault
    /// before the mark retains record, index entry and reminder, so events are never dropped.
    /// </summary>
    private async Task CompleteDrainExhaustionAsync(
        AggregateIdentity identity,
        string trackingId,
        UnpublishedEventsRecord record,
        int maxDrainAttempts,
        Activity? activity) {
        string drainedIdentity = record.GetTrackingIdentity(trackingId);
        _ = (activity?.SetTag("eventstore.failure_reason", DrainReasonCodes.AttemptsExhausted));
        _ = (activity?.SetStatus(ActivityStatusCode.Error, DrainReasonCodes.AttemptsExhausted));

        if (!record.DeadLettered) {
            bool published;
            try {
                published = await deadLetterPublisher
                    .PublishDeadLetterAsync(
                        identity,
                        DeadLetterMessage.FromDrainExhaustion(identity, record, trackingId, record.RetryCount))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                published = false;
                logger.LogError(
                    ex,
                    "Drain exhaustion dead-letter publication threw: CorrelationId={CorrelationId}, TrackingId={TrackingId}",
                    record.CorrelationId,
                    drainedIdentity);
            }

            if (!published) {
                // Sink unavailable: retain the record, the index entry and the reminder. Committed
                // events are never dropped just because the exhaustion sink is down.
                Log.DrainExhaustionRetained(
                    logger,
                    record.CorrelationId,
                    drainedIdentity,
                    record.RetryCount);
                return;
            }

            UnpublishedEventsRecord markedRecord = record.MarkDeadLettered(
                DrainReasonCodes.AttemptsExhausted);
            await StateManager.SetStateAsync(
                UnpublishedEventsRecord.GetStateKey(trackingId),
                markedRecord).ConfigureAwait(false);
            try
            {
                await StateManager.SaveStateAsync().ConfigureAwait(false);
            }
            catch (Exception markerSaveException)
            {
                bool markerCommitted = await InspectDrainMarkerSaveFailureAsync(
                    trackingId,
                    record,
                    markedRecord,
                    markerSaveException).ConfigureAwait(false);
                if (!markerCommitted)
                {
                    throw;
                }
            }

            record = markedRecord;
        }

        // One atomic batch: record removal, index-entry removal, the Recoverable -> Terminal
        // disposition transition and the pending-slot decrement commit together, which is what
        // keeps the decrement exactly-once across a retried exhaustion turn.
        await StateManager.RemoveStateAsync(UnpublishedEventsRecord.GetStateKey(trackingId))
            .ConfigureAwait(false);
        UnpublishedPublicationIndex remainingOwners =
            await StagePublicationIndexRemovalAsync(drainedIdentity).ConfigureAwait(false);
        _ = await CompleteRecoverableIdempotencyAsync(drainedIdentity).ConfigureAwait(false);
        await StagePendingCommandCountAsync(remainingOwners.OwnerCount).ConfigureAwait(false);
        try
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupSaveException)
        {
            bool cleanupCommitted = await InspectDrainCleanupSaveFailureAsync(
                trackingId,
                record,
                cleanupSaveException).ConfigureAwait(false);
            if (!cleanupCommitted)
            {
                throw;
            }
        }

        try {
            await UnregisterReminderAsync(UnpublishedEventsRecord.GetReminderName(trackingId))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogWarning(
                ex,
                "Failed to unregister drain reminder after attempt exhaustion: CorrelationId={CorrelationId}",
                record.CorrelationId);
        }

        await WriteDrainAdvisoryStatusAsync(
            identity,
            record,
            drainedIdentity,
            CommandStatus.PublishFailed,
            DrainReasonCodes.AttemptsExhausted,
            retryable: false,
            recoveryReasonCode: DrainReasonCodes.AttemptsExhausted,
            attemptCount: record.RetryCount).ConfigureAwait(false);

        Log.DrainAttemptsExhausted(
            logger,
            record.CorrelationId,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            record.RetryCount,
            maxDrainAttempts,
            record.EventCount);
    }

    /// <summary>
    /// Writes the advisory recovery status for a drain outcome. Failures are logged, never thrown
    /// (rule #12).
    /// </summary>
    private async Task WriteDrainAdvisoryStatusAsync(
        AggregateIdentity identity,
        UnpublishedEventsRecord record,
        string statusKey,
        CommandStatus status,
        string? failureReason,
        bool retryable,
        string? recoveryReasonCode,
        int attemptCount) {
        try {
            await commandStatusStore.WriteStatusAsync(
                identity.TenantId,
                statusKey,
                new CommandStatusRecord(
                    status,
                    DateTimeOffset.UtcNow,
                    identity.AggregateId,
                    EventCount: record.EventCount,
                    RejectionEventType: null,
                    FailureReason: failureReason,
                    TimeoutDuration: null,
                    MessageId: record.MessageId,
                    CorrelationId: record.CorrelationId,
                    Retryable: retryable,
                    RecoveryReasonCode: recoveryReasonCode,
                    DrainAttemptCount: attemptCount)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            // Rule #12: Advisory status writes -- failure logged, never thrown.
            logger.LogWarning(
                ex,
                "Advisory drain status write failed: CorrelationId={CorrelationId}, Status={Status}",
                record.CorrelationId,
                status);
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

    // Missing legacy state represents zero; activation and the fail-closed barrier reconcile this
    // projection against the normalized publication-owner index before other state access proceeds.
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

    /// <summary>
    /// Reconciles the pending-command projection to the fresh normalized publication-owner count
    /// from a clean cache boundary and resolves an ambiguous save exactly once. A post-commit throw
    /// is observed without a second save; a pre-commit failure is repaired by one clean restage/save.
    /// </summary>
    private async Task FinalizePendingCommandAsync(CommandEnvelope command, Activity? processActivity)
    {
        const string clearOperation = "PendingFinalizerClear";
        const string ownerReadOperation = "PendingFinalizerOwnerRead";
        const string readOperation = "PendingFinalizerRead";
        const string writeOperation = "PendingFinalizerWrite";
        const string saveOperation = "PendingFinalizerSave";

        string operation = clearOperation;
        int committedBefore = -1;
        int expectedAfter = -1;
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;

            operation = ownerReadOperation;
            UnpublishedPublicationIndex publicationOwners = await ReadPublicationIndexAsync()
                .ConfigureAwait(false);
            expectedAfter = publicationOwners.OwnerCount;

            operation = readOperation;
            committedBefore = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            if (committedBefore == expectedAfter)
            {
                ClearPendingFinalizerRecovery();
                return;
            }

            operation = writeOperation;
            await StagePendingCommandCountAsync(expectedAfter).ConfigureAwait(false);

            operation = saveOperation;
            await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
            ClearPendingFinalizerRecovery();
        }
        catch (OperationCanceledException ex)
        {
            (bool discarded, int observed, string consequence, string recoveryExceptionType) =
                await InspectPendingFinalizerFailureAsync(
                    committedBefore,
                    expectedAfter,
                    allowRecovery: false).ConfigureAwait(false);
            RecordPendingFinalizationFailure(
                command,
                processActivity,
                operation,
                ex,
                discarded,
                committedBefore,
                expectedAfter,
                observed,
                consequence,
                recoveryExceptionType);
            throw;
        }
        catch (Exception ex)
        {
            (bool discarded, int observed, string consequence, string recoveryExceptionType) =
                await InspectPendingFinalizerFailureAsync(
                    committedBefore,
                    expectedAfter,
                    allowRecovery: true).ConfigureAwait(false);
            RecordPendingFinalizationFailure(
                command,
                processActivity,
                operation,
                ex,
                discarded,
                committedBefore,
                expectedAfter,
                observed,
                consequence,
                recoveryExceptionType);
        }
    }

    private async Task<(bool Discarded, int Observed, string Consequence, string RecoveryExceptionType)>
        InspectPendingFinalizerFailureAsync(
            int committedBefore,
            int expectedAfter,
            bool allowRecovery)
    {
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
        }
        catch (Exception clearException)
        {
            _stateCacheUnsafe = true;
            if (expectedAfter >= 0)
            {
                RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
            }
            else
            {
                ClearPendingFinalizerRecovery();
                _pendingCountReconciliationRequired = true;
            }

            return (false, -1, "CacheDiscardUnproved", clearException.GetType().Name);
        }

        if (expectedAfter < 0)
        {
            try
            {
                expectedAfter = (await ReadPublicationIndexAsync().ConfigureAwait(false)).OwnerCount;
            }
            catch (Exception ownerInspectionException)
            {
                _stateCacheUnsafe = true;
                ClearPendingFinalizerRecovery();
                _pendingCountReconciliationRequired = true;
                return (
                    true,
                    -1,
                    "PublicationOwnerCountInspectionFailed",
                    ownerInspectionException.GetType().Name);
            }
        }

        if (committedBefore < 0 && !allowRecovery)
        {
            RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
            return (true, -1, "NoPendingMutationStaged", "None");
        }

        int observed;
        try
        {
            observed = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            if (committedBefore < 0)
            {
                committedBefore = observed;
            }
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
            return (true, -1, "DurableInspectionFailed", inspectionException.GetType().Name);
        }

        if (observed == expectedAfter)
        {
            ClearPendingFinalizerRecovery();
            return (true, observed, "CommitObserved", "None");
        }

        if (observed != committedBefore)
        {
            _stateCacheUnsafe = true;
            RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
            return (true, observed, "UnexpectedPendingCount", "None");
        }

        if (!allowRecovery)
        {
            RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
            return (true, observed, "CommitNotObserved", "None");
        }

        try
        {
            await StagePendingCommandCountAsync(expectedAfter).ConfigureAwait(false);
            await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
            ClearPendingFinalizerRecovery();
            return (true, expectedAfter, "RecoveredPreCommitFailure", "None");
        }
        catch (Exception recoveryException)
        {
            bool recoveryDiscarded = false;
            try
            {
                await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
                recoveryDiscarded = true;
                _stateCacheUnsafe = false;
                int recoveryObserved = await ReadPendingCommandCountAsync().ConfigureAwait(false);
                if (recoveryObserved != expectedAfter)
                {
                    _stateCacheUnsafe = true;
                    RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
                }
                else
                {
                    ClearPendingFinalizerRecovery();
                }

                return (
                    true,
                    recoveryObserved,
                    recoveryObserved == expectedAfter
                        ? "RecoveryCommitObserved"
                        : "RecoveryCommitNotObserved",
                    recoveryException.GetType().Name);
            }
            catch (Exception inspectionException)
            {
                _stateCacheUnsafe = true;
                RequirePendingFinalizerRecovery(committedBefore, expectedAfter);
                return (
                    recoveryDiscarded,
                    -1,
                    recoveryDiscarded ? "RecoveryInspectionFailed" : "RecoveryDiscardUnproved",
                    inspectionException.GetType().Name);
            }
        }
    }

    private void RecordPendingFinalizationFailure(
        CommandEnvelope command,
        Activity? processActivity,
        string operation,
        Exception exception,
        bool failedBatchDiscarded,
        int committedBefore,
        int expectedAfter,
        int observed,
        string durableConsequence,
        string recoveryExceptionType)
    {
        ProtectedDataDiagnosticRedactor.RecordActivityException(processActivity, exception, "pipeline");
        Log.PendingCommandFinalizationFailed(
            logger,
            Host.Id.GetId(),
            command.CorrelationId,
            operation,
            exception.GetType().Name,
            failedBatchDiscarded,
            committedBefore,
            expectedAfter,
            observed,
            durableConsequence,
            recoveryExceptionType);
    }

    private void RequirePendingFinalizerRecovery(int committedBefore, int expectedAfter)
    {
        _pendingFinalizerRecoveryRequired = true;
        _pendingFinalizerCommittedBefore = committedBefore;
        _pendingFinalizerExpectedAfter = expectedAfter;
    }

    private void ClearPendingFinalizerRecovery()
    {
        _pendingFinalizerRecoveryRequired = false;
        _pendingFinalizerCommittedBefore = -1;
        _pendingFinalizerExpectedAfter = -1;
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

    /// <returns>
    /// <c>true</c> when a publication-recovery entry is tracked for this range, meaning activation
    /// will re-arm the drain even if reminder registration fails.
    /// </returns>
    private async Task<bool> StoreDrainRecordAndRegisterReminderAsync(
        string trackingId,
        UnpublishedEventsRecord record) {
        // Stage the drain record (committed with the same SaveStateAsync batch)
        await StateManager.SetStateAsync(
            UnpublishedEventsRecord.GetStateKey(trackingId),
            record).ConfigureAwait(false);

        // Story 4.4: this method is the single choke point covering ALL THREE drain-record creation
        // sites (first-pass publish failure, stale-checkpoint handoff, resume publish failure), so
        // staging the recovery entry here is what makes every drain record re-armable.
        PublicationIndexAddOutcome outcome = await TryStagePublicationIndexEntryAsync(
            trackingId,
            record.CorrelationId).ConfigureAwait(false);
        if (outcome == PublicationIndexAddOutcome.Added) {
            return true;
        }

        // The events are already committed on every path that reaches here, so refusing is not
        // an option; record the loss of the crash-window backstop instead. The drain record and
        // its reminder still drive publication. Keep AtCapacity and InvalidEntry diagnostics apart —
        // collapsing them made capacity look like the cause of blank-identity refusals.
        if (outcome == PublicationIndexAddOutcome.InvalidEntry) {
            Log.PublicationIndexEntryInvalid(
                logger,
                Host.Id.GetId(),
                trackingId,
                record.CorrelationId);
        }
        else {
            Log.PublicationIndexEntryRejected(
                logger,
                Host.Id.GetId(),
                trackingId,
                MaxOutstandingPublicationEntries);
        }

        return false;
    }

    /// <summary>Reads the single fixed publication-recovery index key, defaulting to an empty index.</summary>
    private async Task<UnpublishedPublicationIndex> ReadPublicationIndexAsync() {
        ConditionalValue<UnpublishedPublicationIndex> result = await StateManager
            .TryGetStateAsync<UnpublishedPublicationIndex>(UnpublishedPublicationIndex.StateKey)
            .ConfigureAwait(false);

        return result.HasValue && result.Value is not null
            ? result.Value
            : UnpublishedPublicationIndex.Empty;
    }

    /// <summary>
    /// Stages an index entry into the caller's existing batch. Never adds a round trip of its own
    /// and never commits: the caller's <c>SaveStateAsync</c> does that.
    /// </summary>
    /// <returns>Why the entry is tracked, or which of the two distinct refusals applied.</returns>
    private async Task<PublicationIndexAddOutcome> TryStagePublicationIndexEntryAsync(
        string messageId,
        string correlationId) {
        UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
        var entry = new UnpublishedPublicationEntry(
            messageId,
            correlationId,
            IdempotencyTimeProvider.GetUtcNow());
        PublicationIndexAddOutcome outcome = index.TryAdd(
            entry,
            MaxOutstandingPublicationEntries,
            out UnpublishedPublicationIndex updated);
        if (outcome != PublicationIndexAddOutcome.Added) {
            return outcome;
        }

        await StateManager.SetStateAsync(UnpublishedPublicationIndex.StateKey, updated).ConfigureAwait(false);
        return PublicationIndexAddOutcome.Added;
    }

    /// <summary>Stages removal of an index entry into the caller's existing batch.</summary>
    /// <returns>The normalized post-removal owner set staged by this operation.</returns>
    private async Task<UnpublishedPublicationIndex> StagePublicationIndexRemovalAsync(string messageId) {
        UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(messageId)
            || !index.TryRemove(messageId, out UnpublishedPublicationIndex updated)) {
            return index;
        }

        await StateManager.SetStateAsync(UnpublishedPublicationIndex.StateKey, updated).ConfigureAwait(false);
        return updated;
    }

    /// <summary>
    /// Stages the <c>Recoverable</c> -> <c>Terminal</c> idempotency transition for a completed drain.
    /// </summary>
    private async Task<bool> CompleteRecoverableIdempotencyAsync(string messageId) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            return false;
        }

        var checker = new IdempotencyChecker(
            StateManager,
            Host.LoggerFactory.CreateLogger<IdempotencyChecker>(),
            IdempotencyTimeProvider);
        return await checker
            .TryCompleteRecoverableAsync(
                messageId,
                IdempotencyTimeProvider.GetUtcNow().AddSeconds(IdempotencyRetentionSeconds))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Story 4.4 fail-closed branch: discards the staged events rather than committing a range that
    /// recovery could never find. At capacity this surfaces the existing backpressure rejection; an
    /// invalid entry is a data defect and is surfaced under its own reason so operators are never
    /// told "too many pending commands" while the outstanding count sits far below the threshold.
    /// </summary>
    private async Task<CommandProcessingResult> RejectPublicationIndexRefusalAsync(
        CommandEnvelope command,
        string causationId,
        PublicationIndexAddOutcome outcome,
        PipelineState processingPipeline,
        ActorStateMachine stateMachine,
        string pipelineKeyPrefix,
        Activity? processActivity,
        long startTicks) {
        // Discard everything the persistence step staged. These events must NOT commit.
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
        }
        catch (Exception discardException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                "PublicationIndexRefusal",
                new InvalidOperationException("Publication recovery index refused the staged event batch."),
                "DiscardPublicationIndexRefusalBatch",
                discardException,
                discardException.GetType().Name,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
        int outstanding = index.Entries.Count;
        int threshold = MaxOutstandingPublicationEntries;
        bool atCapacity = outcome == PublicationIndexAddOutcome.AtCapacity;
        string failureReason = atCapacity ? "BackpressureExceeded" : "PublicationIndexEntryInvalid";

        if (atCapacity) {
            Log.BackpressureRejected(
                logger,
                Host.Id.GetId(),
                command.CorrelationId,
                command.TenantId,
                command.Domain,
                command.AggregateId,
                outstanding,
                threshold);
        }
        else {
            Log.PublicationIndexEntryInvalid(
                logger, Host.Id.GetId(), command.MessageId, command.CorrelationId);
        }

        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);
        try
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception cleanupSaveException)
        {
            bool cleanupCommitted = await InspectPipelineCleanupSaveFailureAsync(
                command.CorrelationId,
                $"{pipelineKeyPrefix}{command.CorrelationId}",
                "PublicationIndexRefusal",
                processingPipeline,
                cleanupSaveException).ConfigureAwait(false);
            if (!cleanupCommitted)
            {
                throw;
            }
        }

        await WriteAdvisoryStatusAsync(
            command,
            CommandStatus.Rejected,
            failureReason: failureReason,
            retryable: false,
            recoveryReasonCode: failureReason).ConfigureAwait(false);
        LogStageTransition(CommandStatus.Rejected, command, causationId, startTicks);
        LogCommandCompletedSummary(command, causationId, CommandStatus.Rejected, startTicks);

        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, failureReason));
        return atCapacity
            ? new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: $"Backpressure exceeded: {outstanding} pending commands (threshold: {threshold})",
                CorrelationId: command.CorrelationId,
                BackpressureExceeded: true,
                BackpressurePendingCount: outstanding,
                BackpressureThreshold: threshold,
                FailureReason: failureReason)
            : new CommandProcessingResult(
                Accepted: false,
                ErrorMessage: "Publication recovery entry could not be recorded for the committed range.",
                CorrelationId: command.CorrelationId,
                EventCount: 0,
                FailureReason: failureReason,
                ResultPayloadWithheld: true);
    }

    /// <summary>
    /// Re-arms outstanding entries found at activation.
    /// <para>
    /// Two independent budgets. <see cref="MaxActivationProbeEntries"/> bounds how many entries are
    /// taken into a state-backed recovery path (unarmed drain re-arm or checkpoint rebuild);
    /// already-armed entries are skipped without charging it so a hot armed head cannot starve the
    /// unarmed tail. <see cref="MaxActivationRearmEntries"/> bounds how many successful reminder
    /// registrations or checkpoint rebuilds run in this activation. Failed reminder registration
    /// does not consume the re-arm budget — the entry stays unarmed for the next activation and
    /// later siblings still get a chance in this one.
    /// </para>
    /// <para>
    /// Malformed entries and entries whose target no longer exists are pruned rather than skipped,
    /// because skipping permanently consumes index capacity. Pruning commits the Recoverable →
    /// Terminal idempotency transition immediately so a later handoff failure's
    /// <c>ClearCacheAsync</c> cannot wipe it.
    /// </para>
    /// </summary>
    private async Task RearmOutstandingPublicationsAsync(UnpublishedPublicationIndex index) {
        AggregateIdentity identity = GetAggregateIdentityFromActorId();
        var stateMachine = new ActorStateMachine(
            StateManager,
            Host.LoggerFactory.CreateLogger<ActorStateMachine>());
        string pipelineKeyPrefix = identity.PipelineKeyPrefix;
        var stale = new List<string>();
        int probed = 0;
        int work = 0;

        foreach (UnpublishedPublicationEntry entry in index.Entries) {
            if (work >= MaxActivationRearmEntries) {
                break;
            }

            string messageId = entry.MessageId ?? string.Empty;
            string correlationId = entry.CorrelationId ?? string.Empty;
            if (!entry.IsWellFormed) {
                // Costs no state read and no work: prune and keep scanning.
                // Story 4.4: pruning ends the "events genuinely outstanding" condition that exempts
                // the idempotency record from expiry, so release it in the same batch. A blank
                // message id is handled inside the helper, which returns false without staging.
                await CommitRecoverableCompletionAsync(messageId).ConfigureAwait(false);
                stale.Add(messageId);
                Log.PublicationRecoveryEntryDropped(
                    logger,
                    Host.Id.GetId(),
                    messageId.Length == 0 ? "(none)" : messageId,
                    "malformed_entry");
                continue;
            }

            ConditionalValue<UnpublishedEventsRecord> drainRecord = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(messageId))
                .ConfigureAwait(false);

            if (drainRecord.HasValue) {
                UnpublishedEventsRecord persisted = drainRecord.Value;
                if (persisted.ReminderArmedAt is not null) {
                    // A reminder is already confirmed armed. Re-registering it would reset its due
                    // time to InitialDrainDelay, so an aggregate that activates more often than that
                    // delay would postpone its own drain forever. Do not charge the probe budget —
                    // otherwise a long armed head permanently starves unarmed entries behind it.
                    continue;
                }

                if (probed >= MaxActivationProbeEntries) {
                    break;
                }

                probed++;

                // The drain record committed but the reminder registration may not have survived.
                // Re-register and stamp the record -- RetryCount is left exactly as persisted.
                // Charge the work budget only after confirmed registration so a failing reminder
                // store cannot burn every re-arm slot in one activation.
                if (await RegisterDrainReminderAsync(messageId).ConfigureAwait(false)) {
                    work++;
                    await PersistReminderStampAsync(messageId, persisted).ConfigureAwait(false);
                    Log.PublicationRecoveryReminderRearmed(logger, Host.Id.GetId(), messageId);
                }

                continue;
            }

            if (probed >= MaxActivationProbeEntries) {
                break;
            }

            probed++;

            PipelineState? checkpoint = await stateMachine
                .LoadPipelineStateAsync(pipelineKeyPrefix, correlationId)
                .ConfigureAwait(false);

            if (checkpoint is null) {
                // Nothing left to recover: neither a drain record nor a checkpoint. Drop the entry
                // instead of skipping it -- skipping permanently consumes index capacity.
                // Story 4.4: the events are no longer outstanding, so the Recoverable exemption from
                // bounded expiry must end here too or the record becomes immortal.
                await CommitRecoverableCompletionAsync(messageId).ConfigureAwait(false);
                stale.Add(messageId);
                Log.PublicationRecoveryEntryDropped(
                    logger, Host.Id.GetId(), messageId, "checkpoint_missing");
                continue;
            }

            // Every term can decide on its own. Checking them here means a deterministically
            // inconsistent checkpoint is PRUNED once instead of throwing on every activation forever
            // while holding a re-arm slot and an index capacity slot.
            //
            // CurrentStage is load-bearing and is NOT enforced downstream:
            // HandoffStaleCommittedCheckpointAsync validates only identity, event count and range,
            // and its other caller admits the wider CanRepresentCommittedEvents set (EventsPublished,
            // Completed, PublishFailed). EventsStored is the only stage that means "committed but not
            // yet published", so without this term an already-published checkpoint left behind by a
            // failed terminal batch would be rebuilt into a drain record and republished.
            bool convertible = checkpoint.CurrentStage == CommandStatus.EventsStored
                && string.Equals(checkpoint.MessageId, messageId, StringComparison.Ordinal)
                && checkpoint.EventCount is int checkpointEventCount
                && checkpointEventCount > 0
                && checkpoint.StartSequence is long checkpointStart
                && checkpoint.EndSequence is long checkpointEnd
                && checkpointStart >= 1
                && checkpointEnd >= checkpointStart
                && (checkpointEnd - checkpointStart + 1) == checkpointEventCount;

            if (!convertible) {
                // Never fabricate a range from the mutable stream head.
                // Story 4.4: this entry will never be recovered, so end the Recoverable exemption
                // from bounded expiry along with it.
                await CommitRecoverableCompletionAsync(messageId).ConfigureAwait(false);
                stale.Add(messageId);
                Log.PublicationRecoveryEntryDropped(
                    logger, Host.Id.GetId(), messageId, "checkpoint_incomplete");
                continue;
            }

            work++;
            try {
                await HandoffStaleCommittedCheckpointAsync(checkpoint, stateMachine, pipelineKeyPrefix)
                    .ConfigureAwait(false);
                Log.PublicationRecoveryDrainRecordRebuilt(logger, Host.Id.GetId(), messageId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) {
                // Leave the entry and the checkpoint intact for the next activation. ClearCache only
                // discards uncommitted handoff staging — Recoverable completions were already
                // SaveState'd by CommitRecoverableCompletionAsync above.
                (bool discarded, _) = await TryDiscardFailedBatchAsync().ConfigureAwait(false);
                if (!discarded)
                {
                    throw;
                }

                Log.PublicationRecoveryRearmFailed(
                    logger, Host.Id.GetId(), messageId, ex.GetType().Name);
            }
        }

        if (stale.Count > 0) {
            UnpublishedPublicationIndex pruned = index.Prune(stale);
            await StateManager.SetStateAsync(UnpublishedPublicationIndex.StateKey, pruned)
                .ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stages and commits the <c>Recoverable</c> → <c>Terminal</c> transition so a later
    /// <c>ClearCacheAsync</c> in the same activation cannot discard it.
    /// </summary>
    private async Task CommitRecoverableCompletionAsync(string messageId) {
        if (!await CompleteRecoverableIdempotencyAsync(messageId).ConfigureAwait(false)) {
            return;
        }

        await StateManager.SaveStateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Story 4.4: registers a drain reminder and, only once registration is CONFIRMED, stamps the
    /// just-committed record so a later activation knows a reminder is already live.
    /// <para>
    /// Two design points. First, the order is fail-closed: stamping BEFORE registration would let a
    /// crash in the window leave a record claiming to be armed when it is not, and activation would
    /// skip it forever, so the events would never publish. Stamping only after confirmed success
    /// means the worst case is a redundant re-registration -- one wasted re-arm slot and a reset due
    /// time, never a lost drain.
    /// </para>
    /// <para>
    /// Second, the record is passed in rather than re-read. The caller committed this exact instance
    /// microseconds earlier and an actor turn is single-threaded, so a read could only return the
    /// same value at the cost of a round trip. The extra SetState/SaveState is accepted here because
    /// this is the publish-FAILURE path, not the hot success path.
    /// </para>
    /// </summary>
    /// <param name="trackingId">The drain tracking identifier (the command message id).</param>
    /// <param name="committedRecord">The drain record the caller just committed.</param>
    /// <returns><c>true</c> when the reminder was registered.</returns>
    private async Task<bool> ArmDrainReminderAsync(
        string trackingId,
        UnpublishedEventsRecord committedRecord) {
        if (!await RegisterDrainReminderAsync(trackingId).ConfigureAwait(false)) {
            return false;
        }

        await PersistReminderStampAsync(trackingId, committedRecord).ConfigureAwait(false);
        return true;
    }

    private async Task PersistReminderStampAsync(
        string trackingId,
        UnpublishedEventsRecord committedRecord)
    {
        string stateKey = UnpublishedEventsRecord.GetStateKey(trackingId);
        UnpublishedEventsRecord stamped = committedRecord.MarkReminderArmed(
            IdempotencyTimeProvider.GetUtcNow());
        try
        {
            await StateManager.SetStateAsync(stateKey, stamped).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception saveException)
        {
            (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
                .ConfigureAwait(false);
            if (!discarded)
            {
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "ReminderStamp",
                    saveException,
                    "DiscardReminderStampBatch",
                    saveException,
                    discardExceptionType,
                    failedBatchDiscarded: false,
                    durableStateObservation: "Unobserved");
            }

            try
            {
                ConditionalValue<UnpublishedEventsRecord> observed = await StateManager
                    .TryGetStateAsync<UnpublishedEventsRecord>(stateKey)
                    .ConfigureAwait(false);
                if (observed.HasValue && observed.Value == stamped)
                {
                    return;
                }

                // Registration is already live. A pre-commit stamp failure deliberately leaves
                // the record unarmed so activation may safely re-register it later.
                if (observed.HasValue && observed.Value == committedRecord)
                {
                    return;
                }

                _stateCacheUnsafe = true;
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "ReminderStamp",
                    saveException,
                    "InspectReminderStampCommit",
                    new InvalidOperationException("Reminder stamp durable state is ambiguous."),
                    discardExceptionType,
                    failedBatchDiscarded: true,
                    durableStateObservation: "AmbiguousReminderStamp");
            }
            catch (ActorStateRemediationException)
            {
                throw;
            }
            catch (Exception inspectionException)
            {
                _stateCacheUnsafe = true;
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "ReminderStamp",
                    saveException,
                    "InspectReminderStampCommit",
                    inspectionException,
                    discardExceptionType,
                    failedBatchDiscarded: true,
                    durableStateObservation: "DurableInspectionFailed");
            }
        }
    }

    private async Task<bool> RegisterDrainReminderAsync(string trackingId) {
        try {
            (TimeSpan dueTime, TimeSpan period) = GetDrainReminderSchedule();
            _ = await RegisterReminderAsync(
                UnpublishedEventsRecord.GetReminderName(trackingId),
                null,
                dueTime,
                period).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            logger.LogWarning(
                ex,
                "Drain reminder registration failed: CorrelationId={CorrelationId}. Manual recovery may be needed.",
                trackingId);
            return false;
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
            UnpublishedPublicationIndex currentIndex = await ReadPublicationIndexAsync()
                .ConfigureAwait(false);
            await StagePendingCommandCountAsync(currentIndex.OwnerCount).ConfigureAwait(false);
            try
            {
                await StateManager.SaveStateAsync().ConfigureAwait(false);
            }
            catch (Exception saveException)
            {
                bool committed = await InspectStaleHandoffSaveFailureAsync(
                    stalePipeline,
                    pipelineKeyPrefix,
                    saveException).ConfigureAwait(false);
                if (!committed)
                {
                    throw;
                }
            }

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
            IdempotencyTimeProvider.GetUtcNow(),
            RetryCount: 0,
            LastFailureReason: "stale_checkpoint_handoff",
            MessageId: staleMessageId);

        _ = await StoreDrainRecordAndRegisterReminderAsync(staleMessageId, unpublishedRecord)
            .ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, stalePipeline.CorrelationId)
            .ConfigureAwait(false);
        UnpublishedPublicationIndex updatedIndex = await ReadPublicationIndexAsync().ConfigureAwait(false);
        await StagePendingCommandCountAsync(updatedIndex.OwnerCount).ConfigureAwait(false);
        try
        {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception saveException)
        {
            bool committed = await InspectStaleHandoffSaveFailureAsync(
                stalePipeline,
                pipelineKeyPrefix,
                saveException).ConfigureAwait(false);
            if (!committed)
            {
                throw;
            }
        }

        // Story 4.4: stamp on success so activation does not re-register a live reminder.
        _ = await ArmDrainReminderAsync(staleMessageId, unpublishedRecord).ConfigureAwait(false);

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
            existingPipeline,
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
        bool recoveryEntryTracked = false;
        int drainAttemptCount = 0;
        UnpublishedEventsRecord? committedDrainRecord = null;
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
                string drainTrackingId = existingPipeline.MessageId ?? command.MessageId;

                // Story 4.4: a drain record for this same range may already exist -- resume can run
                // repeatedly for one committed range. Carry its attempt count AND the Story 4.4
                // durability flags forward: resetting DeadLettered would allow a second dead-letter
                // publish, and clearing ReminderArmedAt would force activation to re-register and
                // reset the reminder due time on every resume.
                ConditionalValue<UnpublishedEventsRecord> existingDrain = await StateManager
                    .TryGetStateAsync<UnpublishedEventsRecord>(
                        UnpublishedEventsRecord.GetStateKey(drainTrackingId))
                    .ConfigureAwait(false);
                UnpublishedEventsRecord? priorDrain = existingDrain.HasValue ? existingDrain.Value : null;
                drainAttemptCount = priorDrain?.RetryCount ?? 0;

                var unpublishedRecord = new UnpublishedEventsRecord(
                    command.CorrelationId,
                    startSequence,
                    endSequence,
                    eventCount,
                    command.CommandType,
                    existingPipeline.RejectionEventType is not null,
                    IdempotencyTimeProvider.GetUtcNow(),
                    RetryCount: drainAttemptCount,
                    LastFailureReason: failureReason,
                    MessageId: drainTrackingId,
                    DeadLettered: priorDrain?.DeadLettered ?? false,
                    ReminderArmedAt: priorDrain?.ReminderArmedAt);
                recoveryEntryTracked = await StoreDrainRecordAndRegisterReminderAsync(
                    drainTrackingId,
                    unpublishedRecord).ConfigureAwait(false);
                committedDrainRecord = unpublishedRecord;
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
        catch (Exception ex) {
            bool committed;
            if (committedDrainRecord is not null)
            {
                (committed, _) = await InspectPublicationRecoverySaveFailureAsync(
                    command,
                    committedDrainRecord,
                    failResult,
                    idempotencyChecker,
                    pipelineKeyPrefix,
                    ex).ConfigureAwait(false);
            }
            else
            {
                committed = await InspectIdempotencyCleanupSaveFailureAsync(
                    command,
                    failResult,
                    idempotencyChecker,
                    $"{pipelineKeyPrefix}{command.CorrelationId}",
                    "PublishFailedRecovery",
                    existingPipeline,
                    IdempotencyCheckOutcome.RetryableRecoverable,
                    ex).ConfigureAwait(false);
            }

            if (!committed)
            {
                if (ex is InvalidOperationException)
                {
                    throw new ConcurrencyConflictException(
                        command.CorrelationId,
                        command.AggregateId,
                        command.TenantId,
                        conflictSource: "StateStore",
                        innerException: ex,
                        messageId: command.MessageId);
                }

                throw;
            }
        }

        // Story 4.2: Register drain reminder AFTER successful commit
        // Story 4.4: and stamp the record, so activation neither resets its schedule nor spends a
        // re-arm slot on a reminder that is already live.
        bool drainReminderArmed = shouldRegisterReminder
            && committedDrainRecord is not null
            && await ArmDrainReminderAsync(
                existingPipeline.MessageId ?? command.MessageId,
                committedDrainRecord).ConfigureAwait(false);

        LogStageTransition(CommandStatus.PublishFailed, command, causationId, startTicks);

        // Story 4.4 (AC3): same retryability contract as the first-pass PublishFailed status.
        await WriteAdvisoryStatusAsync(
            command,
            CommandStatus.PublishFailed,
            failureReason,
            existingPipeline.EventCount,
            existingPipeline.RejectionEventType,
            retryable: drainReminderArmed || recoveryEntryTracked,
            recoveryReasonCode: DrainReasonCodes.PublishFailed,
            drainAttemptCount: drainAttemptCount).ConfigureAwait(false);
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
            ResultPayload: resultPayload,
            RejectionEventType: rejectionEventType,
            FailureReason: failureReason,
            ResultPayloadWithheld: !accepted || !string.IsNullOrWhiteSpace(failureReason));
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

    private async ValueTask EnsureExecutionFenceAsync(
        IdempotencyExecutionContext? executionContext,
        CommandEnvelope command,
        CancellationToken cancellationToken)
    {
        if (executionContext is null)
        {
            return;
        }

        IdempotencyExecutionContextProtector protector = executionContextProtector
            ?? throw new InvalidOperationException("Idempotency execution-fence validation is unavailable.");
        await protector.ValidateAsync(executionContext, command, cancellationToken).ConfigureAwait(false);
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
        int? eventCount,
        CancellationToken cancellationToken) {
        string safeFailureReason = ProtectedDataDiagnosticRedactor.RedactException(exception, failureStage.ToString());
        Log.InfrastructureFailure(logger, command.CorrelationId, causationId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, failureStage.ToString(), exception.GetType().Name, safeFailureReason);
        _ = (processActivity?.SetStatus(ActivityStatusCode.Error, "InfrastructureFailure"));

        // Discard any events/metadata staged by the failed persistence step before recording the
        // rejection; otherwise the SaveStateAsync below would durably commit those staged events
        // together with the Rejected result, leaving them persisted but never published. Mirrors the
        // concurrency-conflict path, which already clears the cache before staging its outcome.
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
        }
        catch (Exception remediationException)
        {
            throw await CreateRemediationExceptionAsync(
                command.CorrelationId,
                failureStage.ToString(),
                exception,
                "ClearCache",
                remediationException,
                attemptDiscard: true).ConfigureAwait(false);
        }

        var deadLetterMessage = DeadLetterMessage.FromException(
            command, failureStage, exception, eventCount);

        // Best-effort dead-letter publication (AC #7) -- BEFORE SaveStateAsync (task 6.7)
        bool published;
        try
        {
            published = await deadLetterPublisher
                .PublishDeadLetterAsync(command.AggregateIdentity, deadLetterMessage, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
                .ConfigureAwait(false);
            if (!discarded)
            {
                Log.ActorStateRemediationFailed(
                    logger,
                    Host.Id.GetId(),
                    command.CorrelationId,
                    failureStage.ToString(),
                    exception.GetType().Name,
                    "DeadLetterCancellationCleanup",
                    nameof(OperationCanceledException),
                    discardExceptionType,
                    failedBatchDiscarded: false,
                    durableStateObservation: "Unobserved");
            }

            throw;
        }
        catch (Exception deadLetterException)
        {
            published = false;
            Log.AdvisoryDeadLetterPublicationThrew(
                logger,
                command.CorrelationId,
                failureStage.ToString(),
                exception.GetType().Name,
                deadLetterException.GetType().Name,
                ProtectedDataDiagnosticRedactor.RedactException(
                    deadLetterException,
                    "dead-letter-publication"));
        }
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
        var failResult = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: safeFailureReason,
            CorrelationId: command.CorrelationId,
            EventCount: 0,
            FailureReason: safeFailureReason,
            ResultPayloadWithheld: true);

        string remediationOperation = "CheckpointRejected";
        try
        {
            await stateMachine.CheckpointAsync(pipelineKeyPrefix, rejectedState).ConfigureAwait(false);
            remediationOperation = "CleanupPipeline";
            await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                .ConfigureAwait(false);
            remediationOperation = "SaveRejection";
            await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception remediationException)
        {
            throw await CreateRemediationExceptionAsync(
                command.CorrelationId,
                failureStage.ToString(),
                exception,
                remediationOperation,
                remediationException,
                attemptDiscard: true,
                pipelineStateKey: $"{pipelineKeyPrefix}{command.CorrelationId}").ConfigureAwait(false);
        }

        // Advisory status write (non-blocking per rule #12)
        await WriteAdvisoryStatusAsync(command, CommandStatus.Rejected, safeFailureReason).ConfigureAwait(false);

        LogStageTransition(CommandStatus.Rejected, command, causationId, startTicks);
        LogCommandCompletedSummary(command, causationId, CommandStatus.Rejected, startTicks);
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
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
        }
        catch (Exception remediationException)
        {
            throw await CreateRemediationExceptionAsync(
                command.CorrelationId,
                "PersistenceConflict",
                conflict,
                "ClearCache",
                remediationException,
                attemptDiscard: true).ConfigureAwait(false);
        }

        var result = new CommandProcessingResult(
            Accepted: false,
            ErrorMessage: "ConcurrencyConflict",
            CorrelationId: command.CorrelationId,
            EventCount: 0,
            FailureReason: "ConcurrencyConflict",
            ResultPayloadWithheld: true);

        string remediationOperation = "CleanupPipeline";
        try {
            await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
                .ConfigureAwait(false);
            remediationOperation = "SaveConflictRejection";
            await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception remediationException) {
            throw await CreateRemediationExceptionAsync(
                command.CorrelationId,
                "PersistenceConflict",
                conflict,
                remediationOperation,
                remediationException,
                attemptDiscard: true,
                pipelineStateKey: $"{pipelineKeyPrefix}{command.CorrelationId}").ConfigureAwait(false);
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

    private async Task EnsureStateCacheBarrierAsync(string? correlationId, Activity? activity)
    {
        if (!_stateCacheUnsafe
            && !_pendingFinalizerRecoveryRequired
            && !_pendingCountReconciliationRequired)
        {
            return;
        }

        string remediationOperation = "StateCacheBarrierClear";
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
            if (_pendingFinalizerRecoveryRequired)
            {
                remediationOperation = "StateCacheBarrierPendingRecovery";
                await RecoverPendingFinalizerAtBarrierAsync().ConfigureAwait(false);
            }

            if (_pendingCountReconciliationRequired)
            {
                remediationOperation = "StateCacheBarrierCounterReconciliation";
                await ReconcilePendingCommandCountAsync().ConfigureAwait(false);
            }

            Log.ActorStateCacheBarrierRecovered(
                logger,
                Host.Id.GetId(),
                correlationId ?? "none");
        }
        catch (Exception exception)
        {
            _stateCacheUnsafe = true;
            var remediationException = new ActorStateRemediationException(
                "UnsafeStateCache",
                "UnprovedDiscard",
                remediationOperation,
                exception.GetType().Name,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
            Log.ActorStateRemediationFailed(
                logger,
                Host.Id.GetId(),
                correlationId ?? "none",
                remediationException.PrimaryFailureStage,
                remediationException.PrimaryExceptionType,
                remediationException.RemediationOperation,
                remediationException.RemediationExceptionType,
                remediationException.DiscardExceptionType,
                remediationException.FailedBatchDiscarded,
                remediationException.DurableStateObservation);
            ProtectedDataDiagnosticRedactor.RecordActivityException(
                activity,
                remediationException,
                "pipeline");
            throw remediationException;
        }
    }

    private async Task<bool> InspectProcessingAdmissionSaveFailureAsync(
        CommandEnvelope command,
        PipelineState expectedPipeline,
        string pipelineKeyPrefix,
        int committedBefore,
        int expectedAfter,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                command.CorrelationId,
                "ProcessingAdmission",
                saveException,
                "DiscardProcessingAdmissionBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            ConditionalValue<PipelineState> pipeline = await StateManager
                .TryGetStateAsync<PipelineState>($"{pipelineKeyPrefix}{command.CorrelationId}")
                .ConfigureAwait(false);
            int observedCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            if (pipeline.HasValue
                && pipeline.Value == expectedPipeline
                && observedCount == expectedAfter)
            {
                return true;
            }

            if (!pipeline.HasValue && observedCount == committedBefore)
            {
                return false;
            }

            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                "ProcessingAdmission",
                saveException,
                "InspectProcessingAdmissionCommit",
                new InvalidOperationException("Processing admission durable state is ambiguous."),
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "AmbiguousProcessingAdmission");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                "ProcessingAdmission",
                saveException,
                "InspectProcessingAdmissionCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<bool> InspectDrainCleanupSaveFailureAsync(
        string trackingId,
        UnpublishedEventsRecord record,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                record.CorrelationId,
                "DrainCleanup",
                saveException,
                "DiscardDrainCleanupBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            ConditionalValue<UnpublishedEventsRecord> observedDrain = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(trackingId))
                .ConfigureAwait(false);
            if (observedDrain.HasValue)
            {
                if (observedDrain.Value == record)
                {
                    return false;
                }

                throw new InvalidOperationException("Drain cleanup durable record is ambiguous.");
            }

            UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
            int pendingCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            if (!index.Contains(record.GetTrackingIdentity(trackingId))
                && pendingCount == index.OwnerCount)
            {
                return true;
            }

            throw new InvalidOperationException("Drain cleanup durable ownership is ambiguous.");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                record.CorrelationId,
                "DrainCleanup",
                saveException,
                "InspectDrainCleanupCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<UnpublishedEventsRecord> PersistDrainRetryAsync(
        string trackingId,
        UnpublishedEventsRecord committedRecord,
        UnpublishedEventsRecord updatedRecord)
    {
        string stateKey = UnpublishedEventsRecord.GetStateKey(trackingId);
        try
        {
            await StateManager.SetStateAsync(stateKey, updatedRecord).ConfigureAwait(false);
            await StateManager.SaveStateAsync().ConfigureAwait(false);
            return updatedRecord;
        }
        catch (Exception saveException)
        {
            (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
                .ConfigureAwait(false);
            if (!discarded)
            {
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "DrainRetry",
                    saveException,
                    "DiscardDrainRetryBatch",
                    saveException,
                    discardExceptionType,
                    failedBatchDiscarded: false,
                    durableStateObservation: "Unobserved");
            }

            ConditionalValue<UnpublishedEventsRecord> observed;
            try
            {
                observed = await StateManager
                    .TryGetStateAsync<UnpublishedEventsRecord>(stateKey)
                    .ConfigureAwait(false);
            }
            catch (Exception inspectionException)
            {
                _stateCacheUnsafe = true;
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "DrainRetry",
                    saveException,
                    "InspectDrainRetryCommit",
                    inspectionException,
                    discardExceptionType,
                    failedBatchDiscarded: true,
                    durableStateObservation: "DurableInspectionFailed");
            }

            if (IsExpectedDrainRetry(observed, updatedRecord))
            {
                return observed.Value;
            }

            if (!IsExpectedDrainRetry(observed, committedRecord))
            {
                _stateCacheUnsafe = true;
                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "DrainRetry",
                    saveException,
                    "InspectDrainRetryCommit",
                    new InvalidOperationException("Drain retry durable state is ambiguous."),
                    discardExceptionType,
                    failedBatchDiscarded: true,
                    durableStateObservation: "AmbiguousDrainRetry");
            }

            try
            {
                await StateManager.SetStateAsync(stateKey, updatedRecord).ConfigureAwait(false);
                await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
                return updatedRecord;
            }
            catch (Exception recoveryException)
            {
                (bool recoveryDiscarded, string recoveryDiscardExceptionType) =
                    await TryDiscardFailedBatchAsync().ConfigureAwait(false);
                if (recoveryDiscarded)
                {
                    try
                    {
                        ConditionalValue<UnpublishedEventsRecord> recoveryObserved = await StateManager
                            .TryGetStateAsync<UnpublishedEventsRecord>(stateKey)
                            .ConfigureAwait(false);
                        if (IsExpectedDrainRetry(recoveryObserved, updatedRecord))
                        {
                            return recoveryObserved.Value;
                        }
                    }
                    catch (Exception recoveryInspectionException)
                    {
                        _stateCacheUnsafe = true;
                        throw CreateStateRemediationException(
                            committedRecord.CorrelationId,
                            "DrainRetry",
                            saveException,
                            "InspectDrainRetryRecoveryCommit",
                            recoveryInspectionException,
                            recoveryDiscardExceptionType,
                            failedBatchDiscarded: true,
                            durableStateObservation: "DurableInspectionFailed");
                    }
                }

                throw CreateStateRemediationException(
                    committedRecord.CorrelationId,
                    "DrainRetry",
                    saveException,
                    "RepairDrainRetrySave",
                    recoveryException,
                    recoveryDiscardExceptionType,
                    recoveryDiscarded,
                    recoveryDiscarded ? "RecoveryNotCommitted" : "Unobserved");
            }
        }
    }

    private static bool IsExpectedDrainRetry(
        ConditionalValue<UnpublishedEventsRecord> observed,
        UnpublishedEventsRecord expected)
        => observed.HasValue && observed.Value == expected;

    private async Task<bool> InspectDrainMarkerSaveFailureAsync(
        string trackingId,
        UnpublishedEventsRecord committedRecord,
        UnpublishedEventsRecord markedRecord,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                committedRecord.CorrelationId,
                "DrainExhaustionMarker",
                saveException,
                "DiscardDrainMarkerBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            ConditionalValue<UnpublishedEventsRecord> observed = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(trackingId))
                .ConfigureAwait(false);
            if (observed.HasValue && observed.Value == markedRecord)
            {
                return true;
            }

            if (observed.HasValue && observed.Value == committedRecord)
            {
                return false;
            }

            throw new InvalidOperationException("Drain marker durable state is ambiguous.");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                committedRecord.CorrelationId,
                "DrainExhaustionMarker",
                saveException,
                "InspectDrainMarkerCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<(bool BatchCommitted, bool RecoveryOwnerCommitted)>
        InspectPublicationRecoverySaveFailureAsync(
            CommandEnvelope command,
            UnpublishedEventsRecord expectedDrain,
            CommandProcessingResult expectedResult,
            IdempotencyChecker idempotencyChecker,
            string pipelineKeyPrefix,
            Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                command.CorrelationId,
                "PublishFailedRecovery",
                saveException,
                "DiscardPublicationRecoveryBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            string trackingId = expectedDrain.GetTrackingIdentity(command.MessageId);
            ConditionalValue<PipelineState> pipeline = await StateManager
                .TryGetStateAsync<PipelineState>($"{pipelineKeyPrefix}{command.CorrelationId}")
                .ConfigureAwait(false);
            ConditionalValue<UnpublishedEventsRecord> drain = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(trackingId))
                .ConfigureAwait(false);
            UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
            int pendingCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            IdempotencyCheckResult idempotency = await idempotencyChecker
                .InspectAsync(CreateCommandProcessingIdentity(command))
                .ConfigureAwait(false);
            bool recoveryOwnerCommitted = index.Contains(trackingId);
            bool countConsistent = pendingCount == index.OwnerCount;
            bool exactDrain = drain.HasValue && drain.Value == expectedDrain;
            bool exactRecoverable = idempotency.Outcome == IdempotencyCheckOutcome.RetryableRecoverable
                && idempotency.Result == expectedResult;

            if (!pipeline.HasValue
                && exactDrain
                && exactRecoverable
                && recoveryOwnerCommitted
                && countConsistent)
            {
                return (true, true);
            }

            // A still-present checkpoint is the pre-commit witness. An already committed recovery
            // owner may pre-date this resume attempt and must not be finalized as though this turn
            // had acquired a new pending slot.
            if (pipeline.HasValue
                && CreateCommandProcessingIdentity(command).Matches(pipeline.Value)
                && (recoveryOwnerCommitted
                    ? countConsistent
                    : pendingCount == checked(index.OwnerCount + 1)))
            {
                return (false, recoveryOwnerCommitted);
            }

            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                "PublishFailedRecovery",
                saveException,
                "InspectPublicationRecoveryCommit",
                new InvalidOperationException("Publication recovery durable state is ambiguous."),
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "AmbiguousPublicationRecovery");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                "PublishFailedRecovery",
                saveException,
                "InspectPublicationRecoveryCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<bool> InspectEventBatchSaveFailureAsync(
        CommandEnvelope command,
        PipelineState processingPipeline,
        PipelineState eventsStoredPipeline,
        EventPersistResult persistResult,
        Exception primaryException,
        string primaryFailureStage,
        CancellationToken cancellationToken)
    {
        string discardExceptionType;
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
            discardExceptionType = "None";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            (bool discarded, string retryDiscardExceptionType) = await TryDiscardFailedBatchAsync()
                .ConfigureAwait(false);
            if (!discarded)
            {
                Log.ActorStateRemediationFailed(
                    logger,
                    Host.Id.GetId(),
                    command.CorrelationId,
                    primaryFailureStage,
                    primaryException.GetType().Name,
                    "ClearCacheBeforeRetryCancellation",
                    nameof(OperationCanceledException),
                    retryDiscardExceptionType,
                    failedBatchDiscarded: false,
                    durableStateObservation: "Unobserved");
            }

            throw;
        }
        catch (Exception discardException)
        {
            throw await CreateRemediationExceptionAsync(
                command.CorrelationId,
                primaryFailureStage,
                primaryException,
                "ClearCache",
                discardException,
                attemptDiscard: true).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string pipelineKey = $"{command.AggregateIdentity.PipelineKeyPrefix}{command.CorrelationId}";
            ConditionalValue<PipelineState> pipeline = await StateManager
                .TryGetStateAsync<PipelineState>(pipelineKey)
                .ConfigureAwait(false);
            UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
            bool exactRecoveryOwner = index.Entries.Any(entry =>
                string.Equals(entry.MessageId, command.MessageId, StringComparison.Ordinal)
                && string.Equals(entry.CorrelationId, command.CorrelationId, StringComparison.Ordinal));

            if (pipeline.HasValue
                && pipeline.Value == processingPipeline
                && !index.Contains(command.MessageId))
            {
                return false;
            }

            if (pipeline.HasValue
                && pipeline.Value == eventsStoredPipeline
                && exactRecoveryOwner
                && await HasExactCommittedEventBatchAsync(command, persistResult).ConfigureAwait(false))
            {
                return true;
            }

            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                primaryFailureStage,
                primaryException,
                "InspectEventBatchCommit",
                new InvalidOperationException("Event batch durable state is ambiguous."),
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "AmbiguousEventBatch");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                primaryFailureStage,
                primaryException,
                "InspectEventBatchCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<bool> HasExactCommittedEventBatchAsync(
        CommandEnvelope command,
        EventPersistResult persistResult)
    {
        if (persistResult.PersistedEnvelopes.Count == 0)
        {
            return false;
        }

        EventEnvelope first = persistResult.PersistedEnvelopes[0];
        ConditionalValue<AggregateMetadata> metadata = await StateManager
            .TryGetStateAsync<AggregateMetadata>(command.AggregateIdentity.MetadataKey)
            .ConfigureAwait(false);
        if (!metadata.HasValue
            || metadata.Value != new AggregateMetadata(
                persistResult.NewSequenceNumber,
                first.Timestamp,
                ETag: null))
        {
            return false;
        }

        foreach (EventEnvelope expected in persistResult.PersistedEnvelopes)
        {
            ConditionalValue<EventEnvelope> observed = await StateManager
                .TryGetStateAsync<EventEnvelope>(
                    $"{command.AggregateIdentity.EventStreamKeyPrefix}{expected.SequenceNumber}")
                .ConfigureAwait(false);
            if (!observed.HasValue || !EventEnvelopesMatch(observed.Value, expected))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EventEnvelopesMatch(EventEnvelope observed, EventEnvelope expected)
        => string.Equals(observed.MessageId, expected.MessageId, StringComparison.Ordinal)
            && string.Equals(observed.AggregateId, expected.AggregateId, StringComparison.Ordinal)
            && string.Equals(observed.AggregateType, expected.AggregateType, StringComparison.Ordinal)
            && string.Equals(observed.TenantId, expected.TenantId, StringComparison.Ordinal)
            && string.Equals(observed.Domain, expected.Domain, StringComparison.Ordinal)
            && observed.SequenceNumber == expected.SequenceNumber
            && observed.GlobalPosition == expected.GlobalPosition
            && observed.Timestamp == expected.Timestamp
            && string.Equals(observed.CorrelationId, expected.CorrelationId, StringComparison.Ordinal)
            && string.Equals(observed.CausationId, expected.CausationId, StringComparison.Ordinal)
            && string.Equals(observed.UserId, expected.UserId, StringComparison.Ordinal)
            && string.Equals(observed.DomainServiceVersion, expected.DomainServiceVersion, StringComparison.Ordinal)
            && string.Equals(observed.EventTypeName, expected.EventTypeName, StringComparison.Ordinal)
            && observed.MetadataVersion == expected.MetadataVersion
            && string.Equals(observed.SerializationFormat, expected.SerializationFormat, StringComparison.Ordinal)
            && observed.Payload.AsSpan().SequenceEqual(expected.Payload)
            && EventExtensionsMatch(observed.Extensions, expected.Extensions);

    private static bool EventExtensionsMatch(
        IDictionary<string, string>? observed,
        IDictionary<string, string>? expected)
    {
        if (ReferenceEquals(observed, expected))
        {
            return true;
        }

        if (observed is null || expected is null || observed.Count != expected.Count)
        {
            return false;
        }

        return expected.All(pair =>
            observed.TryGetValue(pair.Key, out string? value)
            && string.Equals(value, pair.Value, StringComparison.Ordinal));
    }

    private async Task<bool> InspectIdempotencyCleanupSaveFailureAsync(
        CommandEnvelope command,
        CommandProcessingResult expectedResult,
        IdempotencyChecker idempotencyChecker,
        string pipelineStateKey,
        string primaryFailureStage,
        PipelineState expectedPreCommitPipeline,
        IdempotencyCheckOutcome expectedCommittedOutcome,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                command.CorrelationId,
                primaryFailureStage,
                saveException,
                "DiscardIdempotencyCleanupBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            CommandProcessingIdentity identity = CreateCommandProcessingIdentity(command);
            IdempotencyCheckResult idempotency = await idempotencyChecker
                .InspectAsync(identity)
                .ConfigureAwait(false);
            ConditionalValue<PipelineState> pipeline = await StateManager
                .TryGetStateAsync<PipelineState>(pipelineStateKey)
                .ConfigureAwait(false);
            if (idempotency.Outcome == expectedCommittedOutcome
                && idempotency.Result == expectedResult
                && !pipeline.HasValue)
            {
                return true;
            }

            bool exactPreCommitPipeline = pipeline.HasValue
                && pipeline.Value == expectedPreCommitPipeline;
            if (idempotency.Outcome == IdempotencyCheckOutcome.Miss && exactPreCommitPipeline)
            {
                return false;
            }

            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                primaryFailureStage,
                saveException,
                "InspectIdempotencyCleanupCommit",
                new InvalidOperationException("Idempotency cleanup durable state is ambiguous."),
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "AmbiguousIdempotencyCleanup");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                command.CorrelationId,
                primaryFailureStage,
                saveException,
                "InspectIdempotencyCleanupCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<bool> InspectPipelineCleanupSaveFailureAsync(
        string correlationId,
        string pipelineStateKey,
        string primaryFailureStage,
        PipelineState expectedPreCommitPipeline,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                correlationId,
                primaryFailureStage,
                saveException,
                "DiscardPipelineCleanupBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            ConditionalValue<PipelineState> observed = await StateManager
                .TryGetStateAsync<PipelineState>(pipelineStateKey)
                .ConfigureAwait(false);
            if (!observed.HasValue)
            {
                return true;
            }

            if (observed.Value == expectedPreCommitPipeline)
            {
                return false;
            }

            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                correlationId,
                primaryFailureStage,
                saveException,
                "InspectPipelineCleanupCommit",
                new InvalidOperationException("Pipeline cleanup durable state is ambiguous."),
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "AmbiguousPipelineCleanup");
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                correlationId,
                primaryFailureStage,
                saveException,
                "InspectPipelineCleanupCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task<bool> InspectStaleHandoffSaveFailureAsync(
        PipelineState stalePipeline,
        string pipelineKeyPrefix,
        Exception saveException)
    {
        (bool discarded, string discardExceptionType) = await TryDiscardFailedBatchAsync()
            .ConfigureAwait(false);
        if (!discarded)
        {
            throw CreateStateRemediationException(
                stalePipeline.CorrelationId,
                "StaleCheckpointHandoff",
                saveException,
                "DiscardStaleHandoffBatch",
                saveException,
                discardExceptionType,
                failedBatchDiscarded: false,
                durableStateObservation: "Unobserved");
        }

        try
        {
            ConditionalValue<PipelineState> pipeline = await StateManager
                .TryGetStateAsync<PipelineState>($"{pipelineKeyPrefix}{stalePipeline.CorrelationId}")
                .ConfigureAwait(false);
            if (pipeline.HasValue)
            {
                return false;
            }

            UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
            int pendingCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
            if (pendingCount != index.OwnerCount)
            {
                _stateCacheUnsafe = true;
                throw new InvalidOperationException("Stale handoff owner count is ambiguous.");
            }

            int eventCount = stalePipeline.EventCount ?? 0;
            if (eventCount <= 0)
            {
                return true;
            }

            string messageId = stalePipeline.MessageId
                ?? throw new InvalidOperationException("Stale handoff message identity is missing.");
            ConditionalValue<UnpublishedEventsRecord> drain = await StateManager
                .TryGetStateAsync<UnpublishedEventsRecord>(UnpublishedEventsRecord.GetStateKey(messageId))
                .ConfigureAwait(false);
            return index.Contains(messageId)
                && drain.HasValue
                && drain.Value.MessageId == messageId
                && drain.Value.StartSequence == stalePipeline.StartSequence
                && drain.Value.EndSequence == stalePipeline.EndSequence
                && drain.Value.EventCount == eventCount;
        }
        catch (ActorStateRemediationException)
        {
            throw;
        }
        catch (Exception inspectionException)
        {
            _stateCacheUnsafe = true;
            throw CreateStateRemediationException(
                stalePipeline.CorrelationId,
                "StaleCheckpointHandoff",
                saveException,
                "InspectStaleHandoffCommit",
                inspectionException,
                discardExceptionType,
                failedBatchDiscarded: true,
                durableStateObservation: "DurableInspectionFailed");
        }
    }

    private async Task RecoverPendingFinalizerAtBarrierAsync()
    {
        int observed = await ReadPendingCommandCountAsync().ConfigureAwait(false);
        int expectedAfter = _pendingFinalizerExpectedAfter;
        if (_pendingFinalizerCommittedBefore < 0)
        {
            _pendingFinalizerCommittedBefore = observed;
        }

        if (expectedAfter < 0)
        {
            ClearPendingFinalizerRecovery();
            _pendingCountReconciliationRequired = true;
            return;
        }

        if (observed == expectedAfter)
        {
            ClearPendingFinalizerRecovery();
            return;
        }

        if (observed != _pendingFinalizerCommittedBefore)
        {
            throw new InvalidOperationException("Pending finalizer durable state is ambiguous.");
        }

        await StagePendingCommandCountAsync(expectedAfter).ConfigureAwait(false);
        await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
        ClearPendingFinalizerRecovery();
    }

    private async Task ReconcilePendingCommandCountAsync()
    {
        _pendingCountReconciliationRequired = true;
        await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
        _stateCacheUnsafe = false;

        UnpublishedPublicationIndex index = await ReadPublicationIndexAsync().ConfigureAwait(false);
        int expected = index.OwnerCount;
        int observed = await ReadPendingCommandCountAsync().ConfigureAwait(false);
        if (observed == expected)
        {
            _pendingCountReconciliationRequired = false;
            return;
        }

        await StagePendingCommandCountAsync(expected).ConfigureAwait(false);
        try
        {
            await StateManager.SaveStateAsync(CancellationToken.None).ConfigureAwait(false);
            _pendingCountReconciliationRequired = false;
        }
        catch
        {
            try
            {
                await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
                _stateCacheUnsafe = false;
                UnpublishedPublicationIndex durableIndex = await ReadPublicationIndexAsync()
                    .ConfigureAwait(false);
                int durableCount = await ReadPendingCommandCountAsync().ConfigureAwait(false);
                if (durableCount == durableIndex.OwnerCount)
                {
                    _pendingCountReconciliationRequired = false;
                    return;
                }
            }
            catch
            {
                _stateCacheUnsafe = true;
            }

            _pendingCountReconciliationRequired = true;
            throw;
        }
    }

    private async Task<ActorStateRemediationException> CreateRemediationExceptionAsync(
        string correlationId,
        string primaryFailureStage,
        Exception primaryException,
        string remediationOperation,
        Exception remediationException,
        bool attemptDiscard,
        string? pipelineStateKey = null)
    {
        (bool failedBatchDiscarded, string discardExceptionType) = attemptDiscard
            ? await TryDiscardFailedBatchAsync().ConfigureAwait(false)
            : (false, "NotAttempted");
        if (!failedBatchDiscarded)
        {
            _stateCacheUnsafe = true;
        }

        string durableStateObservation = failedBatchDiscarded
            ? pipelineStateKey is null
                ? "FailedBatchDiscarded"
                : await ObservePipelineCleanupAsync(pipelineStateKey).ConfigureAwait(false)
            : "Unobserved";
        string safeRemediationType = remediationException.GetType().Name;
        Log.ActorStateRemediationFailed(
            logger,
            Host.Id.GetId(),
            correlationId,
            primaryFailureStage,
            primaryException.GetType().Name,
            remediationOperation,
            safeRemediationType,
            discardExceptionType,
            failedBatchDiscarded,
            durableStateObservation);
        return new ActorStateRemediationException(
            primaryFailureStage,
            primaryException.GetType().Name,
            remediationOperation,
            safeRemediationType,
            failedBatchDiscarded,
            durableStateObservation,
            discardExceptionType);
    }

    private ActorStateRemediationException CreateStateRemediationException(
        string correlationId,
        string primaryFailureStage,
        Exception primaryException,
        string remediationOperation,
        Exception remediationException,
        string discardExceptionType,
        bool failedBatchDiscarded,
        string durableStateObservation)
    {
        var exception = new ActorStateRemediationException(
            primaryFailureStage,
            primaryException.GetType().Name,
            remediationOperation,
            remediationException.GetType().Name,
            failedBatchDiscarded,
            durableStateObservation,
            discardExceptionType);
        Log.ActorStateRemediationFailed(
            logger,
            Host.Id.GetId(),
            correlationId,
            exception.PrimaryFailureStage,
            exception.PrimaryExceptionType,
            exception.RemediationOperation,
            exception.RemediationExceptionType,
            exception.DiscardExceptionType,
            exception.FailedBatchDiscarded,
            exception.DurableStateObservation);
        return exception;
    }

    private async Task<(bool Discarded, string ExceptionType)> TryDiscardFailedBatchAsync()
    {
        try
        {
            await StateManager.ClearCacheAsync(CancellationToken.None).ConfigureAwait(false);
            _stateCacheUnsafe = false;
            return (true, "None");
        }
        catch (Exception discardException)
        {
            _stateCacheUnsafe = true;
            return (false, discardException.GetType().Name);
        }
    }

    private async Task<string> ObservePipelineCleanupAsync(string pipelineStateKey)
    {
        try
        {
            ConditionalValue<PipelineState> state = await StateManager
                .TryGetStateAsync<PipelineState>(pipelineStateKey, CancellationToken.None)
                .ConfigureAwait(false);
            return state.HasValue ? "CleanupNotCommitted" : "CleanupCommitted";
        }
        catch (Exception exception)
        {
            _stateCacheUnsafe = true;
            return $"DurableInspectionFailed:{exception.GetType().Name}";
        }
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
        PipelineState expectedPreCommitPipeline,
        Activity? processActivity,
        long startTicks,
        string? rejectionEventType = null,
        string? resultPayload = null) {
        var result = new CommandProcessingResult(
            Accepted: accepted,
            ErrorMessage: errorMessage,
            CorrelationId: command.CorrelationId,
            EventCount: eventCount,
            ResultPayload: resultPayload,
            RejectionEventType: rejectionEventType,
            FailureReason: accepted ? null : "DomainRejected",
            ResultPayloadWithheld: !accepted);

        await RecordIdempotencyAsync(
            idempotencyChecker,
            CreateCommandProcessingIdentity(command),
            result,
            IdempotencyRecordDisposition.Terminal).ConfigureAwait(false);
        await stateMachine.CleanupPipelineAsync(pipelineKeyPrefix, command.CorrelationId)
            .ConfigureAwait(false);

        // Story 4.4: the events reached the broker, so the recovery entry staged into the commit
        // batch is released in the same batch as the terminal cleanup. A failure here must not fail
        // an otherwise successful command -- activation prunes an orphaned entry.
        try {
            _ = await StagePublicationIndexRemovalAsync(command.MessageId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            _pendingCountReconciliationRequired = true;
            Log.PublicationIndexReleaseFailed(
                logger, Host.Id.GetId(), command.MessageId, ex.GetType().Name);
        }

        try {
            await StateManager.SaveStateAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            ConcurrencyConflictException? conflict = ex is InvalidOperationException
                ? new ConcurrencyConflictException(
                    command.CorrelationId,
                    command.AggregateId,
                    command.TenantId,
                    conflictSource: "StateStore",
                    innerException: ex,
                    messageId: command.MessageId)
                : null;
            bool terminalCommitted = await InspectIdempotencyCleanupSaveFailureAsync(
                command,
                result,
                idempotencyChecker,
                $"{pipelineKeyPrefix}{command.CorrelationId}",
                "TerminalCompletion",
                expectedPreCommitPipeline,
                IdempotencyCheckOutcome.ExactTerminalDuplicate,
                conflict ?? ex).ConfigureAwait(false);
            if (!terminalCommitted)
            {
                if (conflict is not null)
                {
                    throw conflict;
                }

                throw;
            }
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
        string? rejectionEventType = null,
        bool? retryable = null,
        string? recoveryReasonCode = null,
        int? drainAttemptCount = null) {
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
                    CorrelationId: command.CorrelationId,
                    Retryable: retryable,
                    RecoveryReasonCode: recoveryReasonCode,
                    DrainAttemptCount: drainAttemptCount)).ConfigureAwait(false);
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

        [LoggerMessage(
            EventId = 2010,
            Level = LogLevel.Warning,
            Message = "Publication recovery activation degraded: ActorId={ActorId}, ExceptionType={ExceptionType}, Stage=PublicationRecoveryDegraded")]
        public static partial void PublicationRecoveryDegraded(
            ILogger logger,
            string actorId,
            string exceptionType);

        [LoggerMessage(
            EventId = 2011,
            Level = LogLevel.Information,
            Message = "Publication recovery re-registered a drain reminder: ActorId={ActorId}, MessageId={MessageId}, Stage=PublicationRecoveryReminderRearmed")]
        public static partial void PublicationRecoveryReminderRearmed(
            ILogger logger,
            string actorId,
            string messageId);

        [LoggerMessage(
            EventId = 2012,
            Level = LogLevel.Information,
            Message = "Publication recovery rebuilt a drain record from a committed checkpoint: ActorId={ActorId}, MessageId={MessageId}, Stage=PublicationRecoveryDrainRecordRebuilt")]
        public static partial void PublicationRecoveryDrainRecordRebuilt(
            ILogger logger,
            string actorId,
            string messageId);

        [LoggerMessage(
            EventId = 2013,
            Level = LogLevel.Warning,
            Message = "Publication recovery dropped a stale index entry: ActorId={ActorId}, MessageId={MessageId}, ReasonCode={ReasonCode}, Stage=PublicationRecoveryEntryDropped")]
        public static partial void PublicationRecoveryEntryDropped(
            ILogger logger,
            string actorId,
            string messageId,
            string reasonCode);

        [LoggerMessage(
            EventId = 2014,
            Level = LogLevel.Warning,
            Message = "Publication recovery re-arm failed and will retry on the next activation: ActorId={ActorId}, MessageId={MessageId}, ExceptionType={ExceptionType}, Stage=PublicationRecoveryRearmFailed")]
        public static partial void PublicationRecoveryRearmFailed(
            ILogger logger,
            string actorId,
            string messageId,
            string exceptionType);

        [LoggerMessage(
            EventId = 2015,
            Level = LogLevel.Error,
            Message = "Publication recovery index refused an entry for an already-committed range: ActorId={ActorId}, MessageId={MessageId}, Threshold={Threshold}, Stage=PublicationIndexEntryRejected")]
        public static partial void PublicationIndexEntryRejected(
            ILogger logger,
            string actorId,
            string messageId,
            int threshold);

        [LoggerMessage(
            EventId = 2019,
            Level = LogLevel.Error,
            Message = "Publication recovery entry identity was invalid, committed range refused: ActorId={ActorId}, MessageId={MessageId}, CorrelationId={CorrelationId}, Stage=PublicationIndexEntryInvalid")]
        public static partial void PublicationIndexEntryInvalid(
            ILogger logger,
            string actorId,
            string messageId,
            string correlationId);

        [LoggerMessage(
            EventId = 2016,
            Level = LogLevel.Warning,
            Message = "Publication recovery index release failed: ActorId={ActorId}, MessageId={MessageId}, ExceptionType={ExceptionType}, Stage=PublicationIndexReleaseFailed")]
        public static partial void PublicationIndexReleaseFailed(
            ILogger logger,
            string actorId,
            string messageId,
            string exceptionType);

        [LoggerMessage(
            EventId = 2017,
            Level = LogLevel.Error,
            Message = "Drain attempts exhausted, events dead-lettered: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, RetryCount={RetryCount}, MaxDrainAttempts={MaxDrainAttempts}, EventCount={EventCount}, Stage=DrainAttemptsExhausted")]
        public static partial void DrainAttemptsExhausted(
            ILogger logger,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId,
            int retryCount,
            int maxDrainAttempts,
            int eventCount);

        [LoggerMessage(
            EventId = 2018,
            Level = LogLevel.Error,
            Message = "Drain exhaustion sink unavailable, drain record retained: CorrelationId={CorrelationId}, MessageId={MessageId}, RetryCount={RetryCount}, Stage=DrainExhaustionRetained")]
        public static partial void DrainExhaustionRetained(
            ILogger logger,
            string correlationId,
            string messageId,
            int retryCount);

        [LoggerMessage(
            EventId = 2020,
            Level = LogLevel.Error,
            Message = "Actor state remediation failed: ActorId={ActorId}, CorrelationId={CorrelationId}, PrimaryFailureStage={PrimaryFailureStage}, PrimaryExceptionType={PrimaryExceptionType}, RemediationOperation={RemediationOperation}, RemediationExceptionType={RemediationExceptionType}, DiscardExceptionType={DiscardExceptionType}, FailedBatchDiscarded={FailedBatchDiscarded}, DurableStateObservation={DurableStateObservation}, Stage=ActorStateRemediationFailed")]
        public static partial void ActorStateRemediationFailed(
            ILogger logger,
            string actorId,
            string correlationId,
            string primaryFailureStage,
            string primaryExceptionType,
            string remediationOperation,
            string remediationExceptionType,
            string discardExceptionType,
            bool failedBatchDiscarded,
            string durableStateObservation);

        [LoggerMessage(
            EventId = 2021,
            Level = LogLevel.Error,
            Message = "Advisory dead-letter publication threw: CorrelationId={CorrelationId}, PrimaryFailureStage={PrimaryFailureStage}, PrimaryExceptionType={PrimaryExceptionType}, DeadLetterExceptionType={DeadLetterExceptionType}, FailureReason={FailureReason}, Stage=AdvisoryDeadLetterPublicationFailed")]
        public static partial void AdvisoryDeadLetterPublicationThrew(
            ILogger logger,
            string correlationId,
            string primaryFailureStage,
            string primaryExceptionType,
            string deadLetterExceptionType,
            string failureReason);

        [LoggerMessage(
            EventId = 2022,
            Level = LogLevel.Warning,
            Message = "Pending command finalization failed: ActorId={ActorId}, CorrelationId={CorrelationId}, Operation={Operation}, ExceptionType={ExceptionType}, FailedBatchDiscarded={FailedBatchDiscarded}, CommittedBefore={CommittedBefore}, ExpectedAfter={ExpectedAfter}, ObservedPendingCount={ObservedPendingCount}, DurableStateObservation={DurableStateObservation}, RecoveryExceptionType={RecoveryExceptionType}, Stage=PendingCommandFinalizationFailed")]
        public static partial void PendingCommandFinalizationFailed(
            ILogger logger,
            string actorId,
            string correlationId,
            string operation,
            string exceptionType,
            bool failedBatchDiscarded,
            int committedBefore,
            int expectedAfter,
            int observedPendingCount,
            string durableStateObservation,
            string recoveryExceptionType);

        [LoggerMessage(
            EventId = 2023,
            Level = LogLevel.Information,
            Message = "Actor state cache barrier recovered: ActorId={ActorId}, CorrelationId={CorrelationId}, Stage=ActorStateCacheBarrierRecovered")]
        public static partial void ActorStateCacheBarrierRecovered(
            ILogger logger,
            string actorId,
            string correlationId);
    }
}
