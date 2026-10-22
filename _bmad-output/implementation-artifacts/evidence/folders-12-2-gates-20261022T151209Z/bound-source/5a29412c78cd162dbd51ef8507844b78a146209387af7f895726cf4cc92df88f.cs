using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulCommandRouter(ProviderStateCoordinator coordinator) : ICommandRouter
{
    public Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandProcessingResult result = SupportedProviderStates.RequireActive(coordinator) switch
        {
            "command-not-found" => new(false, "Synthetic bounded contract fixture.", command.CorrelationId, RejectionEventType: "OrderNotFound"),
            "command-conflict" => new(false, "ConcurrencyConflict", command.CorrelationId, FailureReason: "ConcurrencyConflict"),
            "command-rate-limited" => new(
                false,
                "Backpressure",
                command.CorrelationId,
                BackpressureExceeded: true,
                BackpressurePendingCount: 2,
                BackpressureThreshold: 1),
            "command-unexpected-5xx" => throw new InvalidOperationException("provider-verification-synthetic-failure"),
            "command-accepted" or "command-validation-failure" or "command-unauthorized"
                or "command-forbidden" or "command-auth-tenant" => new(true, CorrelationId: command.CorrelationId),
            _ => throw new InvalidOperationException("provider-command-state-unsupported"),
        };
        return Task.FromResult(result);
    }

    public Task<CommandProcessingResult> RouteFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
        => RouteCommandAsync(command, cancellationToken);

    public Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss));
    }
}
