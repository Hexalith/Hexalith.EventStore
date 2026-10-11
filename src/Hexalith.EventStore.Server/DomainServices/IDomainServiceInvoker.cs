
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.DomainServices;
/// <summary>
/// Minimal contract for invoking a domain service to process a command.
/// The server calls domain services via DAPR; in tests, this is faked.
/// </summary>
public interface IDomainServiceInvoker {
    /// <summary>Invokes a domain service with an optional request for post-command folded state.</summary>
    /// <param name="command">The command envelope.</param>
    /// <param name="currentState">The current aggregate state.</param>
    /// <param name="includePostCommandState">Whether folded-state evidence is requested.</param>
    /// <param name="cancellationToken">The originating cancellation token.</param>
    /// <returns>The domain result; older implementations omit optional state evidence.</returns>
    Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, bool includePostCommandState, CancellationToken cancellationToken = default)
        => InvokeAsync(command, currentState, cancellationToken);

    /// <summary>Invokes with the exact deployment-owned capture qualification identifier.</summary>
    Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, bool includePostCommandState,
        string? snapshotCaptureProfileId, CancellationToken cancellationToken = default)
        => InvokeAsync(command, currentState, includePostCommandState, cancellationToken);

    /// <summary>
    /// Invokes a domain service to process the specified command against the current aggregate state.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <param name="currentState">The current aggregate state, or null for new aggregates.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="DomainResult"/> containing the resulting domain events.</returns>
    Task<DomainResult> InvokeAsync(CommandEnvelope command, object? currentState, CancellationToken cancellationToken = default);
}
