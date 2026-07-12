using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Server.Pipeline;

/// <summary>
/// Handles command submission: writes status, archives, and routes to the aggregate actor.
/// Story 2.6: Writes "Received" status to state store (advisory, rule #12).
/// Story 2.7: Archives original command for replay (advisory, rule #12).
/// Story 3.1: Routes command to aggregate actor via CommandRouter.
/// </summary>
public partial class SubmitCommandHandler(
    ICommandStatusStore statusStore,
    ICommandArchiveStore archiveStore,
    ICommandRouter commandRouter,
    ICommandActivityTracker? activityTracker,
    IStreamActivityTracker? streamActivityTracker,
    IProjectionUpdateOrchestrator projectionOrchestrator,
    ILogger<SubmitCommandHandler> logger,
    ICommandCorrelationIndex? correlationIndex = null) : IRequestHandler<SubmitCommand, SubmitCommandResult> {

    public SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ILogger<SubmitCommandHandler> logger)
        : this(statusStore, archiveStore, commandRouter, null, null, new NoOpProjectionUpdateOrchestrator(), logger) { }

    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string causationId = request.MessageId;

        Log.CommandReceived(logger, request.MessageId, request.CorrelationId, causationId, request.CommandType, request.Tenant, request.Domain, request.AggregateId);

        var result = new SubmitCommandResult(request.CorrelationId, MessageId: request.MessageId);

        CommandProcessingResult processingResult = await commandRouter.RouteCommandAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!processingResult.Accepted
            && string.Equals(processingResult.ErrorMessage, "command_identity_conflict", StringComparison.Ordinal)) {
            throw new CommandIdentityConflictException(
                request.MessageId,
                request.CorrelationId,
                request.Tenant);
        }

        CommandStatusRecord? observedStatus = null;
        bool statusReadSucceeded = false;
        try {
            observedStatus = await statusStore
                .ReadStatusAsync(request.Tenant, request.MessageId, cancellationToken)
                .ConfigureAwait(false);
            statusReadSucceeded = true;

            if (observedStatus is not null
                && !string.Equals(observedStatus.MessageId, request.MessageId, StringComparison.Ordinal)) {
                throw new CommandIdentityConflictException(
                    request.MessageId,
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

        if (statusReadSucceeded
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
                    MessageId: request.MessageId,
                    CorrelationId: request.CorrelationId);
                await statusStore.WriteStatusAsync(
                    request.Tenant,
                    request.MessageId,
                    observedStatus,
                    cancellationToken).ConfigureAwait(false);
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
            ArchivedCommand? archived = await archiveStore
                .ReadCommandAsync(request.Tenant, request.MessageId, cancellationToken)
                .ConfigureAwait(false);
            if (archived is not null) {
                if (!string.Equals(archived.MessageId, request.MessageId, StringComparison.Ordinal)
                    || !string.Equals(archived.CommandType, request.CommandType, StringComparison.Ordinal)) {
                    throw new CommandIdentityConflictException(
                        request.MessageId,
                        request.CorrelationId,
                        request.Tenant);
                }
            }
            else {
                await archiveStore.WriteCommandAsync(
                    request.Tenant,
                    request.MessageId,
                    request.ToArchivedCommand(),
                    cancellationToken).ConfigureAwait(false);
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
                CommandCorrelationIndexAddOutcome indexOutcome = await correlationIndex.AddAsync(
                    request.Tenant,
                    request.CorrelationId,
                    request.MessageId,
                    cancellationToken).ConfigureAwait(false);
                if (indexOutcome is CommandCorrelationIndexAddOutcome.Overflow
                    or CommandCorrelationIndexAddOutcome.RetryExhausted) {
                    Log.CorrelationIndexMaintenanceFailed(
                        logger,
                        request.MessageId,
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
                    request.MessageId,
                    request.CorrelationId,
                    request.Tenant);
            }
        }

        if (processingResult.Accepted && processingResult.EventCount > 0) {
            await TriggerProjectionUpdateAsync(request).ConfigureAwait(false);
        }

        // Read final status once for both activity trackers (advisory, rule #12)
        CommandStatusRecord? finalStatus = observedStatus;

        // Track command in admin activity index (advisory, rule #12)
        if (activityTracker is not null) {
            try {
                await activityTracker.TrackAsync(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    request.CorrelationId,
                    request.CommandType,
                    finalStatus?.Status ?? (processingResult.Accepted ? CommandStatus.Completed : CommandStatus.Rejected),
                    finalStatus?.Timestamp ?? DateTimeOffset.UtcNow,
                    finalStatus?.EventCount,
                    finalStatus?.FailureReason,
                    cancellationToken).ConfigureAwait(false);
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
                await streamActivityTracker.TrackAsync(
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    finalStatus!.EventCount!.Value,
                    finalStatus.Timestamp,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                Log.StreamActivityTrackingFailed(logger, ex, request.CorrelationId, request.Tenant);
            }
        }

        if (!processingResult.Accepted) {
            if (processingResult.BackpressureExceeded) {
                throw new Hexalith.EventStore.Server.Actors.BackpressureExceededException(
                    request.CorrelationId,
                    request.Tenant,
                    request.Domain,
                    request.AggregateId,
                    pendingCount: processingResult.BackpressurePendingCount ?? 0,
                    threshold: processingResult.BackpressureThreshold ?? 0);
            }

            CommandStatusRecord? status = await statusStore
                .ReadStatusAsync(request.Tenant, request.MessageId, cancellationToken)
                .ConfigureAwait(false);

            if (status is { RejectionEventType: not null }) {
                throw new DomainCommandRejectedException(
                    request.CorrelationId,
                    request.Tenant,
                    status.RejectionEventType,
                    processingResult.ErrorMessage ?? $"Domain rejection: {status.RejectionEventType}");
            }

            if (string.Equals(status?.FailureReason, "ConcurrencyConflict", StringComparison.Ordinal)
                || string.Equals(processingResult.ErrorMessage, "ConcurrencyConflict", StringComparison.Ordinal)) {
                throw new ConcurrencyConflictException(
                    request.CorrelationId,
                    request.AggregateId,
                    request.Tenant,
                    conflictSource: "StateStore",
                    messageId: request.MessageId);
            }

            throw new InvalidOperationException(processingResult.ErrorMessage ?? "Command processing was rejected.");
        }

        Log.CommandRouted(logger, request.CorrelationId);

        string? resultPayload = null;
        if (processingResult.Accepted && processingResult.ResultPayload is not null) {
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

    private async Task TriggerProjectionUpdateAsync(SubmitCommand request) {
        try {
            await projectionOrchestrator
                .UpdateProjectionAsync(
                    new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ProjectionUpdateFailed(logger, ex, request.CorrelationId, request.Tenant, request.Domain, request.AggregateId);
        }
    }

    private static partial class Log {

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
            Message = "Command received: MessageId={MessageId}, CorrelationId={CorrelationId}, CausationId={CausationId}, CommandType={CommandType}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, Stage=CommandReceived")]
        public static partial void CommandReceived(
            ILogger logger,
            string messageId,
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
