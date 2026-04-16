using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

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
    ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult> {
    public SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ILogger<SubmitCommandHandler> logger)
        : this(statusStore, archiveStore, commandRouter, null, (IStreamActivityTracker?)null, logger) { }

    public SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        IBackpressureTracker backpressureTracker,
        ILogger<SubmitCommandHandler> logger)
        : this(statusStore, archiveStore, commandRouter, null, (IStreamActivityTracker?)null, logger) => ArgumentNullException.ThrowIfNull(backpressureTracker);

    public SubmitCommandHandler(
        ICommandStatusStore statusStore,
        ICommandArchiveStore archiveStore,
        ICommandRouter commandRouter,
        ICommandActivityTracker? activityTracker,
        IBackpressureTracker backpressureTracker,
        ILogger<SubmitCommandHandler> logger)
        : this(statusStore, archiveStore, commandRouter, activityTracker, (IStreamActivityTracker?)null, logger) => ArgumentNullException.ThrowIfNull(backpressureTracker);

    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string causationId = request.MessageId;

        Log.CommandReceived(logger, request.MessageId, request.CorrelationId, causationId, request.CommandType, request.Tenant, request.Domain, request.AggregateId);

        var result = new SubmitCommandResult(request.CorrelationId);

        try {
            await statusStore.WriteStatusAsync(
                request.Tenant,
                request.CorrelationId,
                new CommandStatusRecord(
                    CommandStatus.Received,
                    DateTimeOffset.UtcNow,
                    request.AggregateId,
                    EventCount: null,
                    RejectionEventType: null,
                    FailureReason: null,
                    TimeoutDuration: null),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            Log.StatusWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
        }

        try {
            await archiveStore.WriteCommandAsync(
                request.Tenant,
                request.CorrelationId,
                request.ToArchivedCommand(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            Log.ArchiveWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
        }

        CommandProcessingResult processingResult = await commandRouter.RouteCommandAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Read final status once for both activity trackers (advisory, rule #12)
        CommandStatusRecord? finalStatus = null;
        if (activityTracker is not null || streamActivityTracker is not null) {
            try {
                finalStatus = await statusStore
                    .ReadStatusAsync(request.Tenant, request.CorrelationId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                Log.StatusReadForTrackingFailed(logger, ex, request.CorrelationId, request.Tenant);
            }
        }

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
                .ReadStatusAsync(request.Tenant, request.CorrelationId, cancellationToken)
                .ConfigureAwait(false);

            if (status is { RejectionEventType: not null }) {
                throw new DomainCommandRejectedException(
                    request.CorrelationId,
                    request.Tenant,
                    status.RejectionEventType,
                    processingResult.ErrorMessage ?? $"Domain rejection: {status.RejectionEventType}");
            }

            throw new InvalidOperationException(processingResult.ErrorMessage ?? "Command processing was rejected.");
        }

        Log.CommandRouted(logger, request.CorrelationId);

        return result;
    }

    private static partial class Log {
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
            EventId = 1101,
            Level = LogLevel.Warning,
            Message = "Failed to write command status: CorrelationId={CorrelationId}, TenantId={TenantId}. Status tracking may be incomplete. Command processing continues. Stage=StatusWriteFailed")]
        public static partial void StatusWriteFailed(
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
            EventId = 1103,
            Level = LogLevel.Debug,
            Message = "Command routed to actor: CorrelationId={CorrelationId}, Stage=CommandRouted")]
        public static partial void CommandRouted(
            ILogger logger,
            string correlationId);

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
            EventId = 1105,
            Level = LogLevel.Warning,
            Message = "Failed to track stream activity: CorrelationId={CorrelationId}, TenantId={TenantId}. Admin stream index may be stale. Stage=StreamActivityTrackingFailed")]
        public static partial void StreamActivityTrackingFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);

        [LoggerMessage(
            EventId = 1106,
            Level = LogLevel.Warning,
            Message = "Failed to read final status for activity tracking: CorrelationId={CorrelationId}, TenantId={TenantId}. Activity tracking skipped. Stage=StatusReadForTrackingFailed")]
        public static partial void StatusReadForTrackingFailed(
            ILogger logger,
            Exception ex,
            string correlationId,
            string tenantId);
    }
}
