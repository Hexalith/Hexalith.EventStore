
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;

namespace Hexalith.EventStore.Sample.Counter;
/// <summary>
/// Domain processor for the Counter aggregate. Implements the pure function contract:
/// (Command, CurrentState?) -> DomainResult with three possible outcomes:
/// success events, rejection events, or no-op.
/// </summary>
public sealed class CounterProcessor : DomainProcessorBase<CounterState> {
    /// <inheritdoc/>
    protected override Task<DomainResult> HandleAsync(CommandEnvelope command, CounterState? currentState) {
        ArgumentNullException.ThrowIfNull(command);

        // Deserialize command.Payload to the concrete command type (D3 pattern).
        // Even for parameterless commands, this validates the payload is well-formed JSON.
        DomainResult result = command.CommandType switch {
            nameof(IncrementCounter) => HandleIncrement(
                DeserializePayload<IncrementCounter>(command)),
            nameof(DecrementCounter) => HandleDecrement(
                DeserializePayload<DecrementCounter>(command),
                currentState),
            nameof(ResetCounter) => HandleReset(
                DeserializePayload<ResetCounter>(command),
                currentState),
            _ => throw new InvalidOperationException($"Unknown command type: '{command.CommandType}'."),
        };

        return Task.FromResult(result);
    }

    private static T DeserializePayload<T>(CommandEnvelope command) =>
        command.Payload.Length == 0
            ? throw new InvalidOperationException(
                $"Command '{command.CommandType}' has an empty payload. Expected valid JSON for {typeof(T).Name}.")
            : JsonSerializer.Deserialize<T>(command.Payload)
              ?? throw new InvalidOperationException(
                  $"Failed to deserialize payload for command '{command.CommandType}' to {typeof(T).Name}.");

    private static DomainResult HandleIncrement(IncrementCounter _)
        => DomainResult.Success(new IEventPayload[] { new CounterIncremented() });

    private static DomainResult HandleDecrement(DecrementCounter _, CounterState? currentState) {
        if (currentState is null || currentState.Count == 0) {
            return DomainResult.Rejection(new IRejectionEvent[] { new CounterCannotGoNegative() });
        }

        return DomainResult.Success(new IEventPayload[] { new CounterDecremented() });
    }

    private static DomainResult HandleReset(ResetCounter _, CounterState? currentState) {
        if (currentState is null || currentState.Count == 0) {
            return DomainResult.NoOp();
        }

        return DomainResult.Success(new IEventPayload[] { new CounterReset() });
    }
}
