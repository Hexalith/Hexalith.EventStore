
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
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
    IBackpressureTracker backpressureTracker,
    ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Compute canonical actor ID for backpressure tracking (same as CommandRouter line 28)
        string actorId = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId).ActorId;

        string causationId = request.MessageId; // CausationId traces from the specific command (MessageId) that caused the events

        Log.CommandReceived(logger, request.MessageId, request.CorrelationId, causationId, request.CommandType, request.Tenant, request.Domain, request.AggregateId);

        var result = new SubmitCommandResult(request.CorrelationId);

        // CRITICAL: Acquire INSIDE try block to prevent cancellation leak (Story 4.3)
        bool acquired = false;
        try
        {
            acquired = backpressureTracker.TryAcquire(actorId);
            if (!acquired)
            {
                Log.BackpressureExceeded(logger, request.CorrelationId, request.MessageId, request.Tenant, request.Domain, request.AggregateId, request.CommandType, actorId);
                int currentDepth = backpressureTracker.GetCurrentDepth(actorId);
                throw new BackpressureExceededException(actorId, request.Tenant, request.CorrelationId, currentDepth);
            }

            // Write "Received" status before returning (advisory per rule #12)
            try
            {
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.StatusWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
            }

            // Archive original command for replay (advisory per rule #12)
            try
            {
                await archiveStore.WriteCommandAsync(
                    request.Tenant,
                    request.CorrelationId,
                    request.ToArchivedCommand(),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.ArchiveWriteFailed(logger, ex, request.CorrelationId, request.Tenant);
            }

            // Route to aggregate actor (NOT advisory -- failure must propagate)
            CommandProcessingResult processingResult = await commandRouter.RouteCommandAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!processingResult.Accepted)
            {
                CommandStatusRecord? status = await statusStore
                    .ReadStatusAsync(request.Tenant, request.CorrelationId, cancellationToken)
                    .ConfigureAwait(false);

                if (status is { RejectionEventType: not null })
                {
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
        finally
        {
            if (acquired)
            {
                backpressureTracker.Release(actorId);
            }
        }
    }

    private static partial class Log
    {
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
            EventId = 1110,
            Level = LogLevel.Warning,
            Message = "Backpressure exceeded: CorrelationId={CorrelationId}, MessageId={MessageId}, TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, CommandType={CommandType}, ActorId={ActorId}, Stage=BackpressureExceeded")]
        public static partial void BackpressureExceeded(
            ILogger logger,
            string correlationId,
            string messageId,
            string tenantId,
            string domain,
            string aggregateId,
            string commandType,
            string actorId);
    }
}
