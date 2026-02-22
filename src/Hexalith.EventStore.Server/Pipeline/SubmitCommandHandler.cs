namespace Hexalith.EventStore.Server.Pipeline;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.Extensions.Logging;

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
    ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult> {
    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string causationId = request.CorrelationId; // For original submissions, CausationId = CorrelationId

        Log.CommandReceived(logger, request.CorrelationId, causationId, request.CommandType, request.Tenant, request.Domain, request.AggregateId);

        var result = new SubmitCommandResult(request.CorrelationId);

        // Write "Received" status before returning (advisory per rule #12)
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

        // Archive original command for replay (advisory per rule #12)
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

        // Route to aggregate actor (NOT advisory -- failure must propagate)
        await commandRouter.RouteCommandAsync(request, cancellationToken)
            .ConfigureAwait(false);

        Log.CommandRouted(logger, request.CorrelationId);

        return result;
    }

    private static partial class Log {
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
    }
}
