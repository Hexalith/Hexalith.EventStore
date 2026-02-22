namespace Hexalith.EventStore.Server.Commands;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// Routes commands to the correct aggregate actor based on canonical identity.
/// </summary>
public interface ICommandRouter {
    /// <summary>
    /// Routes a submit command to the appropriate aggregate actor.
    /// </summary>
    /// <param name="command">The command to route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result from the actor.</returns>
    Task<CommandProcessingResult> RouteCommandAsync(SubmitCommand command, CancellationToken cancellationToken = default);
}
