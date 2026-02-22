namespace Hexalith.EventStore.Sample.Counter;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

/// <summary>
/// Domain processor for the Counter aggregate. Implements the pure function contract:
/// (Command, CurrentState?) -> DomainResult with three possible outcomes:
/// success events, rejection events, or no-op.
/// </summary>
public sealed class CounterProcessor : DomainProcessorBase<CounterState> {
    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CounterState? currentState) {
        ArgumentNullException.ThrowIfNull(command);

        DomainResult result = command.CommandType switch {
            nameof(Commands.IncrementCounter) => HandleIncrement(),
            nameof(Commands.DecrementCounter) => HandleDecrement(currentState),
            nameof(Commands.ResetCounter) => HandleReset(currentState),
            _ => throw new InvalidOperationException($"Unknown command type: '{command.CommandType}'."),
        };

        return Task.FromResult(result);
    }

    private static DomainResult HandleIncrement()
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    private static DomainResult HandleDecrement(CounterState? currentState) {
        if (currentState is null || currentState.Count == 0) {
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        }

        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    private static DomainResult HandleReset(CounterState? currentState) {
        if (currentState is null || currentState.Count == 0) {
            return DomainResult.NoOp();
        }

        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }
}
