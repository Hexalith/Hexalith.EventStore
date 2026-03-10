
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.DomainServices;
/// <summary>
/// Minimal contract for invoking a domain service to process a command.
/// The server calls domain services via DAPR; in tests, this is faked.
/// </summary>
public interface IDomainServiceInvoker {
    /// <summary>
    /// Invokes a domain service to process the specified command against the current aggregate state.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The current aggregate state, or null for new aggregates.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DomainResult"/> containing the resulting domain events.</returns>
    Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default);
}
