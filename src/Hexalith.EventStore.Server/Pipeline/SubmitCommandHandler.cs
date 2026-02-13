namespace Hexalith.EventStore.Server.Pipeline;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.Extensions.Logging;

/// <summary>
/// Stub handler for Story 2.1 — logs receipt and returns the correlation ID.
/// Story 2.6: Writes "Received" status to state store before returning (advisory, rule #12).
/// Story 2.7: Archives original command for replay (advisory, rule #12).
/// Full command processing (CommandEnvelope creation, actor invocation) comes in Story 3.1.
/// </summary>
public class SubmitCommandHandler(
    ICommandStatusStore statusStore,
    ICommandArchiveStore archiveStore,
    ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public async Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Command received: {CommandType} for {Tenant}/{Domain}/{AggregateId}, CorrelationId={CorrelationId}",
            request.CommandType,
            request.Tenant,
            request.Domain,
            request.AggregateId,
            request.CorrelationId);

        var result = new SubmitCommandResult(request.CorrelationId);

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
            logger.LogWarning(
                ex,
                "Failed to write command status for {CorrelationId}, TenantId={TenantId}. Status tracking may be incomplete. Command processing continues.",
                request.CorrelationId,
                request.Tenant);
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
            logger.LogWarning(
                ex,
                "Failed to archive command for {CorrelationId}, TenantId={TenantId}. Replay may be unavailable. Command processing continues.",
                request.CorrelationId,
                request.Tenant);
        }

        return result;
    }
}
