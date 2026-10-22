
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// Fake command router for integration testing that returns success without DAPR actor infrastructure.
/// </summary>
public class FakeCommandRouter : ICommandRouter {
    /// <summary>Gets or sets the fake actor to delegate to. If null, returns default success.</summary>
    public FakeAggregateActor? FakeActor { get; set; }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> RouteCommandAsync(
        SubmitCommand command,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);

        if (FakeActor is not null) {
            // Let conversion exceptions propagate -- they indicate malformed commands
            // that should have been caught by validation (matches production behavior)
            var envelope = SubmitCommandExtensions.ToCommandEnvelope(command);

            // Actor exceptions should propagate (not caught)
            return FakeActor.ProcessCommandAsync(envelope, cancellationToken);
        }

        return Task.FromResult(new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId));
    }

    /// <inheritdoc/>
    public Task<CommandProcessingResult> RouteFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        if (executionContext.FencingToken <= 0
            || !string.Equals(executionContext.MessageId, command.MessageId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CorrelationId, command.CorrelationId, StringComparison.Ordinal)
            || !string.Equals(executionContext.Tenant, command.Tenant, StringComparison.Ordinal)
            || !string.Equals(executionContext.Domain, command.Domain, StringComparison.Ordinal)
            || !string.Equals(executionContext.AggregateId, command.AggregateId, StringComparison.Ordinal)
            || !string.Equals(executionContext.CommandType, command.CommandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }

        if (FakeActor is not null)
        {
            return FakeActor.ProcessFencedCommandAsync(
                new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext),
                cancellationToken);
        }

        return Task.FromResult(new CommandProcessingResult(Accepted: true, CorrelationId: command.CorrelationId));
    }

    /// <inheritdoc/>
    public Task<IdempotencyCheckResult> ReconcileFencedCommandAsync(
        SubmitCommand command,
        IdempotencyExecutionContext executionContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();
        return FakeActor is not null
            ? FakeActor.ReconcileFencedCommandAsync(
                new FencedCommandEnvelope(command.ToCommandEnvelope(), executionContext))
            : Task.FromResult(new IdempotencyCheckResult(IdempotencyCheckOutcome.Miss));
    }
}
