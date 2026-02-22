
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Handlers;
/// <summary>
/// Pure function contract for domain command processing.
/// Receives a command and optional current state, returns domain events (success, rejection, or no-op).
/// </summary>
public interface IDomainProcessor {
    /// <summary>
    /// Processes a command against optional current aggregate state and returns domain events.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The current aggregate state, or null for new aggregates.</param>
    /// <returns>A <see cref="DomainResult"/> containing the resulting domain events.</returns>
    Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState);
}
