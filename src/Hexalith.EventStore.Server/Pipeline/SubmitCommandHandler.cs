namespace Hexalith.EventStore.Server.Pipeline;

using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.Extensions.Logging;

/// <summary>
/// Stub handler for Story 2.1 — logs receipt and returns the correlation ID.
/// Full command processing (CommandEnvelope creation, actor invocation) comes in Story 3.1.
/// </summary>
public class SubmitCommandHandler(ILogger<SubmitCommandHandler> logger) : IRequestHandler<SubmitCommand, SubmitCommandResult>
{
    public Task<SubmitCommandResult> Handle(SubmitCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Command received: {CommandType} for {Tenant}/{Domain}/{AggregateId}, CorrelationId={CorrelationId}",
            request.CommandType,
            request.Tenant,
            request.Domain,
            request.AggregateId,
            request.CorrelationId);

        return Task.FromResult(new SubmitCommandResult(request.CorrelationId));
    }
}
