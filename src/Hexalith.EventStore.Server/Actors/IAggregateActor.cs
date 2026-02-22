namespace Hexalith.EventStore.Server.Actors;

using Dapr.Actors;

using Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// DAPR actor interface for aggregate command processing.
/// </summary>
public interface IAggregateActor : IActor {
    /// <summary>
    /// Processes a command envelope within the aggregate actor context.
    /// </summary>
    /// <param name="command">The command envelope to process.</param>
    /// <returns>The result of processing the command.</returns>
    Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command);
}
