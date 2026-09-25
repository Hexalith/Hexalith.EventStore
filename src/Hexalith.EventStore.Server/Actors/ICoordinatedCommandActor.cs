using Dapr.Actors;

using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes related cross-stream commands and validates authoritative source state.</summary>
public interface ICoordinatedCommandActor : IActor
{
    /// <summary>Processes a command under the source actor's coordination key.</summary>
    Task<CommandProcessingResult> ProcessCommandAsync(CommandEnvelope command);

    /// <summary>Processes a signed-fence command under the source actor's coordination key.</summary>
    Task<CommandProcessingResult> ProcessFencedCommandAsync(FencedCommandEnvelope request);
}
