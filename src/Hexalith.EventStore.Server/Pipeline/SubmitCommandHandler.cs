using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Diagnostics;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Server.Pipeline;

/// <summary>
/// Handles command submission: writes status, archives, and routes to the aggregate actor.
/// Story 2.6: Writes "Received" status to state store (advisory, rule #12).
/// Story 2.7: Archives original command for replay (advisory, rule #12).
/// Story 3.1: Routes command to aggregate actor via CommandRouter.
/// </summary>
public partial class SubmitCommandHandler : IRequestHandler<SubmitCommand, SubmitCommandResult> {
    private ICommandStatusStore statusStore { get; }
    private ICommandArchiveStore archiveStore { get; }
    private ICommandRouter commandRouter { get; }
    private ICommandActivityTracker? activityTracker { get; }
    private IStreamActivityTracker? streamActivityTracker { get; }
    private IProjectionUpdateOrchestrator projectionOrchestrator { get; }
    private ILogger<SubmitCommandHandler> logger { get; }
    private ICommandCorrelationIndex? correlationIndex { get; }
    private IProjectionActivationOutbox? projectionActivationOutbox { get; }
    private IIdempotencyAdmissionCoordinator? idempotencyAdmissionCoordinator { get; }
    private IConfiguration? humanAuthorityConfiguration { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public SubmitCommandHandler(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ICommandActivityTracker? activityTracker,
        IStreamActivityTracker? streamActivityTracker,
        IProjectionUpdateOrchestrator projectionOrchestrator,
        ILogger<SubmitCommandHandler> logger,
        ICommandCorrelationIndex? correlationIndex = null,
        IProjectionActivationOutbox? projectionActivationOutbox = null,
        IIdempotencyAdmissionCoordinator? idempotencyAdmissionCoordinator = null,
        IConfiguration? humanAuthorityConfiguration = null)
        : this(statusStore, archiveStore, commandRouter, activityTracker, streamActivityTracker, projectionOrchestrator, logger, correlationIndex, projectionActivationOutbox, idempotencyAdmissionCoordinator, humanAuthorityConfiguration, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ICommandActivityTracker? activityTracker,
        IStreamActivityTracker? streamActivityTracker,
        IProjectionUpdateOrchestrator projectionOrchestrator,
        ILogger<SubmitCommandHandler> logger,
        ICommandCorrelationIndex? correlationIndex = null,
        IProjectionActivationOutbox? projectionActivationOutbox = null,
        IIdempotencyAdmissionCoordinator? idempotencyAdmissionCoordinator = null,
        IConfiguration? humanAuthorityConfiguration = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.statusStore = statusStore;
        this.archiveStore = archiveStore;
        this.commandRouter = commandRouter;
        this.activityTracker = activityTracker;
        this.streamActivityTracker = streamActivityTracker;
        this.projectionOrchestrator = projectionOrchestrator;
        this.logger = logger;
        this.correlationIndex = correlationIndex;
        this.projectionActivationOutbox = projectionActivationOutbox;
        this.idempotencyAdmissionCoordinator = idempotencyAdmissionCoordinator;
        this.humanAuthorityConfiguration = humanAuthorityConfiguration;
        this.catalogAuthority = catalogAuthority;

    }


    internal SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ILogger<SubmitCommandHandler> logger)
        : this(statusStore, archiveStore, commandRouter, null, null, new NoOpProjectionUpdateOrchestrator(), logger) { }

    internal SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ILogger<SubmitCommandHandler> logger,
        IIdempotencyAdmissionCoordinator idempotencyAdmissionCoordinator)
        : this(
            statusStore,
            archiveStore,
            commandRouter,
            null,
            null,
            new NoOpProjectionUpdateOrchestrator(),
            logger,
            idempotencyAdmissionCoordinator: idempotencyAdmissionCoordinator) { }

    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        DispatchDiagnostics? diagnostics = DispatchDiagnostics.Create(logger, cancellationToken);
        RouteCatalogDispatchLease? catalogLease;
        using (var stage = diagnostics?.Start(DispatchStageName.SubmitAcquire))
        {
            catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
            stage?.Complete();
        }
        var catalogRelease = new RouteCatalogLeaseReleaser(catalogLease);
        try
        {
            if (catalogLease is not null)
            {
                var selected = catalogLease.Snapshot.Resolve("command", request.Tenant, request.Domain, request.CommandType);
                _ = catalogLease.Snapshot.Admission(selected.Declaration);
            }

            string diagnosticCausationId = request.IdempotencyKey is null ? request.MessageId : "protected";
            Log.CommandReceived(logger, request.CorrelationId, diagnosticCausationId, request.CommandType, request.Tenant, request.Domain, request.AggregateId);

            string executionMessageId = request.MessageId;
            string executionCorrelationId = request.CorrelationId;
            bool reconcileUnknownOutcome = false;
            IdempotencyAdmissionSession? admissionSession = null;
            if (request.IdempotencyKey is not null)
            {
                if (idempotencyAdmissionCoordinator is null)
                {
                    throw new InvalidOperationException("Trusted idempotency admission is unavailable.");
                }

                try
                {
                    using var admissionStage = diagnostics?.Start(DispatchStageName.Admission);
                    admissionSession = await idempotencyAdmissionCoordinator
                        .AdmitAsync(request, cancellationToken)
                        .ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Trusted idempotency admission returned no session.");
                    admissionStage?.Complete();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (IdempotencyAdmissionFailureException)
                {
                    throw;
                }
                catch (ArgumentException exception)
                {
                    logger.LogError(
                        "Trusted idempotency admission rejected the command. ExceptionType={ExceptionType}, CorrelationId={CorrelationId}, Stage=IdempotencyAdmissionRejected",
                        exception.GetType().Name,
                        request.CorrelationId);
                    throw AdmissionFailure(
                        request.CorrelationId,
                        "idempotency_admission_rejected",
                        "validation_failed",
                        retryable: false,
                        "correct_request",
                        400,
                        "Idempotency admission rejected the command. Correct the request and submit a new key.");
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        "Trusted idempotency admission is unavailable. ExceptionType={ExceptionType}, CorrelationId={CorrelationId}, Stage=IdempotencyAdmissionUnavailable",
                        exception.GetType().Name,
                        request.CorrelationId);
                    throw AdmissionFailure(
                        request.CorrelationId,
                        "idempotency_admission_unavailable",
                        "service_unavailable",
                        retryable: true,
                        "retry_later",
                        503,
                        "Idempotency admission is temporarily unavailable. Retry later.");
                }
                switch (admissionSession.Decision)
                {
                    case IdempotencyAdmissionDecision.Conflict:
                        throw new IdempotencyConflictException(request.CorrelationId);
                    case IdempotencyAdmissionDecision.Expired:
                        throw new IdempotencyKeyExpiredException(request.CorrelationId);
                    case IdempotencyAdmissionDecision.Replay:
                        return CreateReplayResult(request, admissionSession);
                    case IdempotencyAdmissionDecision.Execute:
                    case IdempotencyAdmissionDecision.Recoverable:
                        executionMessageId = RequireExecutionIdentity(admissionSession.ExecutionMessageId);
                        executionCorrelationId = RequireCheckpointIdentity(admissionSession.ExecutionCorrelationId);
                        break;
                    case IdempotencyAdmissionDecision.Pending:
                        return new SubmitCommandResult(
                            request.CorrelationId,
                            MessageId: RequireExecutionIdentity(admissionSession.ExecutionMessageId));
                    case IdempotencyAdmissionDecision.UnknownProviderOutcome:
                        executionMessageId = RequireExecutionIdentity(admissionSession.ExecutionMessageId);
                        executionCorrelationId = RequireCheckpointIdentity(admissionSession.ExecutionCorrelationId);
                        reconcileUnknownOutcome = true;
                        break;
                    case IdempotencyAdmissionDecision.Corrupt:
                        throw AdmissionFailure(
                            request.CorrelationId,
                            "idempotency_admission_corrupt",
                            "idempotency_admission_corrupt",
                            retryable: false,
                            "contact_support",
                            503,
                            "Idempotency admission state cannot be safely interpreted.");
                    case IdempotencyAdmissionDecision.Collision:
                        throw AdmissionFailure(
                            request.CorrelationId,
                            "idempotency_key_collision",
                            "idempotency_admission_corrupt",
                            retryable: false,
                            "contact_support",
                            503,
                            "Idempotency admission identity cannot be safely verified.");
                    case IdempotencyAdmissionDecision.Redirect:
                        throw AdmissionFailure(
                            request.CorrelationId,
                            "idempotency_admission_unavailable",
                            "service_unavailable",
                            retryable: true,
                            "retry_later",
                            503,
                            "Idempotency admission authority is temporarily unavailable.");
                    case IdempotencyAdmissionDecision.UnsafeLegacy:
                        throw AdmissionFailure(
                            request.CorrelationId,
                            "idempotency_unsafe_legacy_state",
                            "idempotency_admission_corrupt",
                            retryable: false,
                            "contact_support",
                            503,
                            "Legacy idempotency state is not safe to migrate automatically.");
                    default:
                        throw new InvalidOperationException(
                            $"Idempotency admission denied execution with decision '{admissionSession.Decision}'.");
                }
            }

            SubmitCommand executionRequest = admissionSession is null
                ? request
                : request with
                {
                    MessageId = executionMessageId,
                    CorrelationId = executionCorrelationId,
                    IdempotencyKey = null,
                };
            if (executionRequest.HumanAuthority is HumanAuthoritySource humanAuthority)
            {
                string key = humanAuthorityConfiguration?["Authentication:HumanAuthorityReceipt:SigningKey"]
                    ?? throw new InvalidOperationException("Human authority receipt signing is unavailable.");
                executionRequest = executionRequest with
                {
                    HumanAuthorityReceipt = HumanAuthorityReceiptCodec.Issue(
                        executionRequest.ToCommandEnvelope(),
                        humanAuthority.Tenant,
                        humanAuthority.Subject,
                        humanAuthority.Permissions,
                        key,
                        DateTimeOffset.UtcNow),
                };
            }
            var result = new SubmitCommandResult(request.CorrelationId, MessageId: executionMessageId);
            if (reconcileUnknownOutcome)
            {
                IdempotencyExecutionContext executionContext = admissionSession?.ExecutionContext
                    ?? throw new InvalidOperationException("Unknown idempotency outcome has no reconciliation fence.");
                using var reconciliationFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                await idempotencyAdmissionCoordinator!
                    .ValidateExecutionAsync(admissionSession, executionRequest, cancellationToken)
                    .ConfigureAwait(false);
                reconciliationFenceStage?.Complete();
                IdempotencyCheckResult reconciliation;
                try
                {
                    using var reconciliationStage = diagnostics?.Start(DispatchStageName.Reconciliation);
                    reconciliation = await commandRouter
                        .ReconcileFencedCommandAsync(executionRequest, executionContext, cancellationToken)
                        .ConfigureAwait(false);
                    reconciliationStage?.Complete();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        "Idempotency reconciliation is unavailable. ExceptionType={ExceptionType}, CorrelationId={CorrelationId}, Stage=IdempotencyReconciliationUnavailable",
                        exception.GetType().Name,
                        request.CorrelationId);
                    throw AdmissionFailure(
                        request.CorrelationId,
                        "idempotency_reconciliation_unavailable",
                        "service_unavailable",
                        retryable: true,
                        "retry_later",
                        503,
                        "Idempotency outcome reconciliation is temporarily unavailable. Retry later.");
                }
                if (reconciliation.Outcome is IdempotencyCheckOutcome.ExactTerminalDuplicate
                    or IdempotencyCheckOutcome.RetryableRecoverable)
                {
                    CommandProcessingResult reconciledResult = reconciliation.Result
                        ?? throw new InvalidOperationException("Authoritative reconciliation returned no command result.");
                    using var reconciledFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await idempotencyAdmissionCoordinator
                        .ValidateExecutionAsync(admissionSession, executionRequest, cancellationToken)
                        .ConfigureAwait(false);
                    reconciledFenceStage?.Complete();
                    using var reconciledCompletionStage = diagnostics?.Start(DispatchStageName.AdmissionCompletion);
                    await idempotencyAdmissionCoordinator
                        .CompleteAsync(admissionSession, reconciledResult, cancellationToken)
                        .ConfigureAwait(false);
                    reconciledCompletionStage?.Complete();
                    return CreateReplayResult(
                        request,
                        admissionSession with { ReplayResult = reconciledResult });
                }

                throw AdmissionFailure(
                    request.CorrelationId,
                    "idempotency_outcome_unknown",
                    "idempotency_outcome_unknown",
                    retryable: true,
                    "poll_status_then_retry",
                    409,
                    "The original mutation outcome remains unknown. Poll status before retrying.");
            }

            string diagnosticMessageId = admissionSession is null ? request.MessageId : "protected";

            if (admissionSession is not null && idempotencyAdmissionCoordinator is not null)
            {
                using var capabilityStage = diagnostics?.Start(DispatchStageName.ExecutionCapability);
                await idempotencyAdmissionCoordinator
                    .ValidateExecutionCapabilityAsync(admissionSession, executionRequest, cancellationToken)
                    .ConfigureAwait(false);
                capabilityStage?.Complete();
            }

            CommandProcessingResult processingResult;
            bool sideEffectBoundaryCrossed = false;
            try
            {
                IdempotencyExecutionContext? executionContext = null;
                if (admissionSession is not null && idempotencyAdmissionCoordinator is not null)
                {
                    using var beginStage = diagnostics?.Start(DispatchStageName.ExecutionBegin);
                    await idempotencyAdmissionCoordinator
                        .BeginAsync(admissionSession, cancellationToken)
                        .ConfigureAwait(false);
                    beginStage?.Complete();
                    executionContext = admissionSession.ExecutionContext
                        ?? throw new InvalidOperationException("Executable idempotency admission returned no execution fence.");
                    using var initialFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await idempotencyAdmissionCoordinator
                        .ValidateExecutionAsync(admissionSession, executionRequest, cancellationToken)
                        .ConfigureAwait(false);
                    initialFenceStage?.Complete();
                }

                if (projectionActivationOutbox is not null)
                {
                    // Write-ahead is mandatory: an aggregate commit must never become visible before its
                    // payload-free projection activation can be recovered by another replica.
                    sideEffectBoundaryCrossed = admissionSession is not null;
                    using var activationStage = diagnostics?.Start(DispatchStageName.ProjectionActivation);
                    await projectionActivationOutbox.EnsureAsync(
                        new AggregateIdentity(executionRequest.Tenant, executionRequest.Domain, executionRequest.AggregateId),
                        cancellationToken).ConfigureAwait(false);
                    activationStage?.Complete();
                }

                if (executionContext is null)
                {
                    using var routingStage = diagnostics?.Start(DispatchStageName.Routing);
                    processingResult = await commandRouter.RouteCommandAsync(executionRequest, cancellationToken)
                        .ConfigureAwait(false);
                    routingStage?.Complete();
                }
                else
                {
                    sideEffectBoundaryCrossed = true;
                    using var routingStage = diagnostics?.Start(DispatchStageName.Routing);
                    processingResult = await commandRouter.RouteFencedCommandAsync(
                        executionRequest,
                        executionContext,
                        cancellationToken).ConfigureAwait(false);
                    routingStage?.Complete();
                }
            }
            catch (Exception) when (admissionSession is not null && idempotencyAdmissionCoordinator is not null)
            {
                await MarkRecoveryStateAsync(
                    idempotencyAdmissionCoordinator,
                    admissionSession,
                    sideEffectBoundaryCrossed
                        ? IdempotencyAdmissionState.UnknownProviderOutcome
                        : IdempotencyAdmissionState.Recoverable,
                    logger, diagnostics).ConfigureAwait(false);
                throw;
            }

            if (!processingResult.Accepted
                && (string.Equals(processingResult.ErrorMessage, "command_identity_conflict", StringComparison.Ordinal)
                    || string.Equals(processingResult.ErrorMessage, "idempotency_key_expired", StringComparison.Ordinal)))
            {
                await CompleteAdmissionWithRecoveryAsync(
                    idempotencyAdmissionCoordinator,
                    admissionSession,
                    executionRequest,
                    processingResult,
                    logger,
                    cancellationToken, diagnostics).ConfigureAwait(false);
                ThrowDeterministicFailure(request, executionMessageId, processingResult);
            }

            bool coordinatedConflict = !processingResult.Accepted
                && string.Equals(processingResult.ErrorMessage, "coordinated_source_conflict", StringComparison.Ordinal);
            if (coordinatedConflict)
            {
                try
                {
                    using var conflictStatusStage = diagnostics?.Start(DispatchStageName.StatusWrite);
                    await statusStore.WriteStatusAsync(request.Tenant, executionMessageId,
                        new CommandStatusRecord(CommandStatus.Rejected, DateTimeOffset.UtcNow,
                            request.AggregateId, EventCount: 0, RejectionEventType: null,
                            FailureReason: "ConcurrencyConflict", TimeoutDuration: null,
                            MessageId: executionMessageId, CorrelationId: executionCorrelationId,
                            Retryable: false), cancellationToken).ConfigureAwait(false);
                    conflictStatusStage?.Complete();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Log.StatusWriteFailed(logger, exception, request.CorrelationId, request.Tenant);
                }
            }

            CommandStatusRecord? observedStatus = null;
            bool statusReadSucceeded = false;
            CommandStatusRecord? finalStatus = null;
            try {
                try {
                using var statusReadStage = diagnostics?.Start(DispatchStageName.StatusRead);
                observedStatus = await statusStore
                    .ReadStatusAsync(request.Tenant, executionMessageId, cancellationToken)
                    .ConfigureAwait(false);
                statusReadStage?.Complete();
                statusReadSucceeded = true;

                if (observedStatus is not null
                    && !string.Equals(observedStatus.MessageId, executionMessageId, StringComparison.Ordinal)) {
                    throw new CommandIdentityConflictException(
                        executionMessageId,
                        request.CorrelationId,
                        request.Tenant);
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (CommandIdentityConflictException) {
                throw;
            }
            catch (Exception ex) {
                Log.StatusReadForTrackingFailed(logger, ex, request.CorrelationId, request.Tenant);
            }

            if (!coordinatedConflict && statusReadSucceeded
                && observedStatus is null
                && processingResult.ResultPayload is null) {
                try {
                    observedStatus = new CommandStatusRecord(
                        CommandStatus.Received,
                        DateTimeOffset.UtcNow,
                        request.AggregateId,
                        EventCount: null,
                        RejectionEventType: null,
                        FailureReason: null,
                        TimeoutDuration: null,
                        MessageId: executionMessageId,
                        CorrelationId: executionCorrelationId);
                    using var statusFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await ValidateExecutionFenceAsync(
                        idempotencyAdmissionCoordinator,
                        admissionSession,
                        executionRequest,
                        cancellationToken).ConfigureAwait(false);
                    statusFenceStage?.Complete();
                    using var statusWriteStage = diagnostics?.Start(DispatchStageName.StatusWrite);
                    await statusStore.WriteStatusAsync(
                        request.Tenant,
                        executionMessageId,
                        observedStatus,
                        cancellationToken).ConfigureAwait(false);
                    statusWriteStage?.Complete();
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    observedStatus = null;
                    Log.StatusWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
                }
            }

            try {
                using var archiveReadStage = diagnostics?.Start(DispatchStageName.ArchiveRead);
                ArchivedCommand? archived = await archiveStore
                    .ReadCommandAsync(request.Tenant, executionMessageId, cancellationToken)
                    .ConfigureAwait(false);
                archiveReadStage?.Complete();
                if (archived is not null) {
                    if (!string.Equals(archived.MessageId, executionMessageId, StringComparison.Ordinal)
                        || !string.Equals(archived.CommandType, request.CommandType, StringComparison.Ordinal)) {
                        throw new CommandIdentityConflictException(
                            executionMessageId,
                            request.CorrelationId,
                            request.Tenant);
                    }
                }
                else {
                    using var archiveFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await ValidateExecutionFenceAsync(
                        idempotencyAdmissionCoordinator,
                        admissionSession,
                        executionRequest,
                        cancellationToken).ConfigureAwait(false);
                    archiveFenceStage?.Complete();
                    using var archiveWriteStage = diagnostics?.Start(DispatchStageName.ArchiveWrite);
                    await archiveStore.WriteCommandAsync(
                        request.Tenant,
                        executionMessageId,
                        executionRequest.ToArchivedCommand(),
                        cancellationToken).ConfigureAwait(false);
                    archiveWriteStage?.Complete();
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (CommandIdentityConflictException) {
                throw;
            }
            catch (Exception ex) {
                Log.ArchiveWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
            }

            if (correlationIndex is not null) {
                try {
                    using var correlationFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await ValidateExecutionFenceAsync(
                        idempotencyAdmissionCoordinator,
                        admissionSession,
                        executionRequest,
                        cancellationToken).ConfigureAwait(false);
                    correlationFenceStage?.Complete();
                    using var correlationStage = diagnostics?.Start(DispatchStageName.CorrelationWrite);
                    CommandCorrelationIndexAddOutcome indexOutcome = await correlationIndex.AddAsync(
                        request.Tenant,
                        executionCorrelationId,
                        executionMessageId,
                        cancellationToken).ConfigureAwait(false);
                    correlationStage?.Complete();
                    if (indexOutcome is CommandCorrelationIndexAddOutcome.Overflow
                        or CommandCorrelationIndexAddOutcome.RetryExhausted) {
                        Log.CorrelationIndexMaintenanceFailed(
                            logger,
                            diagnosticMessageId,
                            request.CorrelationId,
                            request.Tenant,
                            indexOutcome.ToString());
                    }
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    Log.CorrelationIndexWriteFailed(
                        logger,
                        ex,
                        diagnosticMessageId,
                        request.CorrelationId,
                        request.Tenant);
                }
            }

            if (processingResult.Accepted && processingResult.EventCount > 0) {
                using var projectionFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                await ValidateExecutionFenceAsync(
                    idempotencyAdmissionCoordinator,
                    admissionSession,
                    executionRequest,
                    cancellationToken).ConfigureAwait(false);
                projectionFenceStage?.Complete();
                await TriggerProjectionUpdateAsync(executionRequest, diagnostics).ConfigureAwait(false);
            }

            // Read final status once for both activity trackers (advisory, rule #12)
            finalStatus = observedStatus;

            // Track command in admin activity index (advisory, rule #12)
            if (activityTracker is not null) {
                try {
                    using var activityFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await ValidateExecutionFenceAsync(
                        idempotencyAdmissionCoordinator,
                        admissionSession,
                        executionRequest,
                        cancellationToken).ConfigureAwait(false);
                    activityFenceStage?.Complete();
                    using var activityStage = diagnostics?.Start(DispatchStageName.ActivityTracking);
                    await activityTracker.TrackAsync(
                        request.Tenant,
                        request.Domain,
                        request.AggregateId,
                        executionCorrelationId,
                        request.CommandType,
                        finalStatus?.Status ?? (processingResult.Accepted ? CommandStatus.Completed : CommandStatus.Rejected),
                        finalStatus?.Timestamp ?? DateTimeOffset.UtcNow,
                        finalStatus?.EventCount,
                        finalStatus?.FailureReason,
                        cancellationToken).ConfigureAwait(false);
                    activityStage?.Complete();
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    Log.ActivityTrackingFailed(logger, ex, request.CorrelationId, request.Tenant);
                }
            }

            // Track stream activity in admin stream index (advisory, rule #12)
            if (streamActivityTracker is not null
                && processingResult.Accepted
                && (finalStatus?.EventCount ?? 0) > 0) {
                try {
                    using var streamFenceStage = diagnostics?.Start(DispatchStageName.ExecutionFence);
                    await ValidateExecutionFenceAsync(
                        idempotencyAdmissionCoordinator,
                        admissionSession,
                        executionRequest,
                        cancellationToken).ConfigureAwait(false);
                    streamFenceStage?.Complete();
                    using var streamStage = diagnostics?.Start(DispatchStageName.StreamTracking);
                    await streamActivityTracker.TrackAsync(
                        request.Tenant,
                        request.Domain,
                        request.AggregateId,
                        finalStatus!.EventCount!.Value,
                        finalStatus.Timestamp,
                        cancellationToken).ConfigureAwait(false);
                    streamStage?.Complete();
                }
                catch (OperationCanceledException) {
                    throw;
                }
                catch (Exception ex) {
                    Log.StreamActivityTrackingFailed(logger, ex, request.CorrelationId, request.Tenant);
                }
            }

            }
            catch (Exception) when (admissionSession is not null && idempotencyAdmissionCoordinator is not null) {
                await MarkRecoveryStateAsync(
                    idempotencyAdmissionCoordinator,
                    admissionSession,
                    IdempotencyAdmissionState.UnknownProviderOutcome,
                    logger, diagnostics).ConfigureAwait(false);
                throw;
            }

            await CompleteAdmissionWithRecoveryAsync(
                idempotencyAdmissionCoordinator,
                admissionSession,
                executionRequest,
                processingResult,
                logger,
                cancellationToken, diagnostics).ConfigureAwait(false);

            if (!processingResult.Accepted) {
                ThrowDeterministicFailure(
                    request,
                    executionMessageId,
                    processingResult,
                    finalStatus?.RejectionEventType,
                    finalStatus?.FailureReason);
            }

            Log.CommandRouted(logger, request.CorrelationId);

            string? resultPayload = null;
            if (processingResult.Accepted && processingResult.ResultPayload is not null) {
                // Gate on the durable status-store read (finalStatus), not processingResult.ResultPayloadWithheld:
                // that flag is the actor's synchronous, in-memory accept/reject decision (used only for idempotency
                // replay below), not confirmation that the async advisory status write reached Completed.
                if (finalStatus?.Status == CommandStatus.Completed) {
                    resultPayload = processingResult.ResultPayload;
                }
                else {
                    Log.ResultPayloadDropped(
                        logger,
                        request.CorrelationId,
                        request.Tenant,
                        request.AggregateId,
                        request.CommandType,
                        finalStatus?.Status.ToString() ?? "Unavailable",
                        statusReadSucceeded);
                }
            }

            return result with {
                ResultPayload = resultPayload,
            };
        }
        finally
        {
            using var stage = diagnostics?.Start(DispatchStageName.SubmitRelease, CancellationToken.None);
            await catalogRelease.DisposeAsync().ConfigureAwait(false);
            stage?.Complete();
        }
    }

    private static string RequireExecutionIdentity(string? executionMessageId)
        => !string.IsNullOrWhiteSpace(executionMessageId)
            ? executionMessageId
            : throw new InvalidOperationException("Live idempotency state has no execution identity.");

    private static string RequireCheckpointIdentity(string? executionCorrelationId)
        => !string.IsNullOrWhiteSpace(executionCorrelationId)
            ? executionCorrelationId
            : throw new InvalidOperationException("Live idempotency state has no checkpoint identity.");

    private static Task ValidateExecutionFenceAsync(
        IIdempotencyAdmissionCoordinator? coordinator,
        IdempotencyAdmissionSession? session,
        SubmitCommand command,
        CancellationToken cancellationToken)
        => coordinator is not null && session is not null
            ? coordinator.ValidateExecutionAsync(session, command, cancellationToken)
            : Task.CompletedTask;

    private static IdempotencyAdmissionFailureException AdmissionFailure(
        string correlationId,
        string code,
        string category,
        bool retryable,
        string clientAction,
        int statusCode,
        string detail)
        => new(correlationId, code, category, retryable, clientAction, statusCode, detail);

    private async Task TriggerProjectionUpdateAsync(SubmitCommand request, DispatchDiagnostics? diagnostics) {
        try {
            using var stage = diagnostics?.Start(DispatchStageName.ProjectionDelivery, CancellationToken.None);
            await projectionOrchestrator
                .UpdateProjectionAsync(
                    new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId),
                    CancellationToken.None)
                .ConfigureAwait(false);
            stage?.Complete();
        }
        catch (Exception ex) {
            Log.ProjectionUpdateFailed(logger, ex, request.CorrelationId, request.Tenant, request.Domain, request.AggregateId);
        }
    }

    private static SubmitCommandResult CreateReplayResult(
        SubmitCommand request,
        IdempotencyAdmissionSession session)
    {
        CommandProcessingResult replay = session.ReplayResult
            ?? throw new InvalidOperationException("Replay admission did not contain a command result.");
        if (!replay.Accepted)
        {
            ThrowDeterministicFailure(
                request,
                RequireExecutionIdentity(session.ExecutionMessageId),
                replay);
        }

        return new SubmitCommandResult(
            request.CorrelationId,
            replay.ResultPayloadWithheld ? null : replay.ResultPayload,
            RequireExecutionIdentity(session.ExecutionMessageId));
    }

    private static void ThrowDeterministicFailure(
        SubmitCommand request,
        string executionMessageId,
        CommandProcessingResult processingResult,
        string? advisoryRejectionEventType = null,
        string? advisoryFailureReason = null)
    {
        if (string.Equals(processingResult.ErrorMessage, "command_identity_conflict", StringComparison.Ordinal))
        {
            throw new CommandIdentityConflictException(
                executionMessageId,
                request.CorrelationId,
                request.Tenant);
        }

        if (string.Equals(processingResult.ErrorMessage, "idempotency_key_expired", StringComparison.Ordinal))
        {
            throw new IdempotencyKeyExpiredException(request.CorrelationId);
        }

        if (Hexalith.EventStore.Server.DomainServices.DomainServiceSafeDenialException.TryParse(
            processingResult.ErrorMessage, out Hexalith.EventStore.Server.DomainServices.DomainServiceSafeDenialException? denial))
        {
            throw denial!;
        }

        if (processingResult.BackpressureExceeded)
        {
            throw new Hexalith.EventStore.Server.Actors.BackpressureExceededException(
                request.CorrelationId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                processingResult.BackpressurePendingCount ?? 0,
                processingResult.BackpressureThreshold ?? 0);
        }

        string? rejectionEventType = processingResult.RejectionEventType ?? advisoryRejectionEventType;
        if (!string.IsNullOrWhiteSpace(rejectionEventType))
        {
            throw new DomainCommandRejectedException(
                request.CorrelationId,
                request.Tenant,
                rejectionEventType,
                processingResult.ErrorMessage ?? $"Domain rejection: {rejectionEventType}");
        }

        if (string.Equals(processingResult.FailureReason, "ConcurrencyConflict", StringComparison.Ordinal)
            || string.Equals(advisoryFailureReason, "ConcurrencyConflict", StringComparison.Ordinal)
            || string.Equals(processingResult.ErrorMessage, "ConcurrencyConflict", StringComparison.Ordinal))
        {
            throw new ConcurrencyConflictException(
                request.CorrelationId,
                request.AggregateId,
                request.Tenant,
                conflictSource: "StateStore",
                messageId: executionMessageId);
        }

        throw new InvalidOperationException(processingResult.ErrorMessage ?? "Command processing was rejected.");
    }

    private static async Task MarkRecoveryStateAsync(
        IIdempotencyAdmissionCoordinator coordinator,
        IdempotencyAdmissionSession session,
        IdempotencyAdmissionState state,
        ILogger logger,
        DispatchDiagnostics? diagnostics)
    {
        try
        {
            using var stage = diagnostics?.Start(DispatchStageName.RecoveryPersistence, CancellationToken.None);
            await coordinator.MarkRecoveryAsync(
                session,
                state,
                CancellationToken.None).ConfigureAwait(false);
            stage?.Complete();
        }
        catch (Exception)
        {
            Log.IdempotencyRecoveryFailed(logger);
        }
    }

    private static async Task CompleteAdmissionWithRecoveryAsync(
        IIdempotencyAdmissionCoordinator? coordinator,
        IdempotencyAdmissionSession? session,
        SubmitCommand command,
        CommandProcessingResult result,
        ILogger logger,
        CancellationToken cancellationToken,
        DispatchDiagnostics? diagnostics)
    {
        if (coordinator is null || session is null)
        {
            return;
        }

        try
        {
            using (var stage = diagnostics?.Start(DispatchStageName.ExecutionFence))
            {
                await coordinator.ValidateExecutionAsync(session, command, cancellationToken).ConfigureAwait(false);
                stage?.Complete();
            }
            using (var stage = diagnostics?.Start(DispatchStageName.AdmissionCompletion))
            {
                await coordinator.CompleteAsync(session, result, cancellationToken).ConfigureAwait(false);
                stage?.Complete();
            }
        }
        catch (Exception)
        {
            await MarkRecoveryStateAsync(
                coordinator,
                session,
                IdempotencyAdmissionState.UnknownProviderOutcome,
                logger, diagnostics).ConfigureAwait(false);
            throw;
        }
    }

    private static partial class Log {

        [LoggerMessage(EventId = 1111, Level = LogLevel.Error,
            Message = "Unable to persist unknown idempotency outcome. Stage=IdempotencyUnknownOutcomePersistenceFailed")]
        public static partial void IdempotencyRecoveryFailed(ILogger logger);


        [LoggerMessage(
            EventId = 1109,
            Level = LogLevel.Warning,
            Message = "Correlation index maintenance did not complete: MessageId={MessageId}, CorrelationId={CorrelationId}, TenantId={TenantId}, Outcome={Outcome}. Message-primary lookup remains authoritative. Stage=CorrelationIndexMaintenanceFailed")]
        public static partial void CorrelationIndexMaintenanceFailed(
            ILogger logger,
            string messageId,
            string correlationId,
            string tenantId,
            string outcome);

        [LoggerMessage(
            EventId = 1110,
            Level = LogLevel.Warning,
            Message = "Correlation index write failed: MessageId={MessageId}, CorrelationId={CorrelationId}, TenantId={TenantId}. Message-primary lookup remains authoritative. Stage=CorrelationIndexWriteFailed")]
        public static partial void CorrelationIndexWriteFailed(
            ILogger logger,
            Exception ex,
            string messageId,
            string correlationId,
            string tenantId);

        [LoggerMessage(
            EventId = 1104,
            Level = LogLevel.Warning,
            Message = "Failed to track command activity: CorrelationId={CorrelationId}, TenantId={TenantId}. Admin command list may be stale. Stage=ActivityTrackingFailed")]
        public static partial void ActivityTrackingFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);

        [LoggerMessage(
            EventId = 1102,
            Level = LogLevel.Warning,
            Message = "Failed to archive command: CorrelationId={CorrelationId}, TenantId={TenantId}. Replay may be unavailable. Command processing continues. Stage=ArchiveWriteFailed")]
        public static partial void ArchiveWriteFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);

        [LoggerMessage(
                            EventId = 1100,
            Level = LogLevel.Information,
            Message = "Command received: CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=CommandReceived")]
        public static partial void CommandReceived(
            ILogger logger,
            string correlationId,
            string causationId,
            string commandType,
            string tenantId,
            string domain,
            string aggregateId);

        [LoggerMessage(
            EventId = 1103,
            Level = LogLevel.Debug,
            Message = "Command routed to actor: CorrelationId={CorrelationId}, Stage=CommandRouted")]
        public static partial void CommandRouted(
            ILogger logger,
            string correlationId);

        [LoggerMessage(
            EventId = 1108,
            Level = LogLevel.Warning,
            Message = "Projection update failed after command routing: CorrelationId={CorrelationId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=ProjectionUpdateFailed")]
        public static partial void ProjectionUpdateFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId,
            string domain,
            string aggregateId);

        [LoggerMessage(
            EventId = 1107,
            Level = LogLevel.Warning,
            Message = "Result payload dropped because final command status was not Completed: CorrelationId={CorrelationId}, TenantId={TenantId}, AggregateId={AggregateId}, CommandType={CommandType}, FinalStatus={FinalStatus}, StatusReadSucceeded={StatusReadSucceeded}. Stage=ResultPayloadDropped")]
        public static partial void ResultPayloadDropped(
            ILogger logger,
            string correlationId,
            string tenantId,
            string aggregateId,
            string commandType,
            string finalStatus,
            bool statusReadSucceeded);

        [LoggerMessage(
            EventId = 1106,
            Level = LogLevel.Warning,
            Message = "Failed to read final status for activity tracking: CorrelationId={CorrelationId}, TenantId={TenantId}. Activity tracking skipped. Stage=StatusReadForTrackingFailed")]
        public static partial void StatusReadForTrackingFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);

        [LoggerMessage(
                                    EventId = 1101,
            Level = LogLevel.Warning,
            Message = "Failed to write command status: CorrelationId={CorrelationId}, TenantId={TenantId}. Status tracking may be incomplete. Command processing continues. Stage=StatusWriteFailed")]
        public static partial void StatusWriteFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);

        [LoggerMessage(
            EventId = 1105,
            Level = LogLevel.Warning,
            Message = "Failed to track stream activity: CorrelationId={CorrelationId}, TenantId={TenantId}. Admin stream index may be stale. Stage=StreamActivityTrackingFailed")]
        public static partial void StreamActivityTrackingFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);
    }
}
