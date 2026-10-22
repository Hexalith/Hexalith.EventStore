using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Processes a domain command while observing the originating request cancellation token.</summary>
public interface IAsyncDomainProcessor
{
    /// <summary>Processes the command against a proven current state.</summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The proven current aggregate state, or null for a new aggregate.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The complete domain result.</returns>
    Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken);
}
