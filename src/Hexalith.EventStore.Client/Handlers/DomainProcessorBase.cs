
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Handlers;
/// <summary>
/// Abstract base class for domain processors that provides typed state casting.
/// Safely converts the untyped <c>object?</c> state to <typeparamref name="TState"/>
/// and delegates to the typed <see cref="HandleAsync"/> method.
/// When state arrives as a <see cref="JsonElement"/> (e.g., from JSON deserialization
/// of <c>object?</c> via DAPR service invocation), it is automatically deserialized
/// to <typeparamref name="TState"/>.
/// </summary>
/// <typeparam name="TState">The aggregate state type. Must be a reference type.</typeparam>
public abstract class DomainProcessorBase<TState> : IDomainProcessor
    where TState : class {
    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) {
        ArgumentNullException.ThrowIfNull(command);
        TState? typedState = currentState switch {
            null => null,
            TState s => s,
            JsonElement je => je.Deserialize<TState>(),
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{currentState.GetType().Name}'."),
        };
        return HandleAsync(command, typedState);
    }

    /// <summary>
    /// Processes a command against optional typed aggregate state and returns domain events.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The current typed aggregate state, or null for new aggregates.</param>
    /// <returns>A <see cref="DomainResult"/> containing the resulting domain events.</returns>
    protected abstract Task<DomainResult> HandleAsync(CommandEnvelope command, TState? currentState);
}
