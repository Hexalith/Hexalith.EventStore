using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Domain-owned policy that places related aggregate commands under one actor turn.</summary>
public interface ICoordinatedCommandPolicy
{
    /// <summary>Returns whether this policy owns the command type and domain.</summary>
    bool Claims(string domain, string commandType);

    /// <summary>Derives the authoritative source stream and coordination key from the command.</summary>
    CoordinatedCommandScope GetScope(CommandEnvelope command);

    /// <summary>Returns a stable digest of semantic command intent for exact rejection retries.</summary>
    string GetCommandDigest(CommandEnvelope command);

    /// <summary>Validates a command against authoritative source events while the coordination turn is held.</summary>
    bool Validate(CommandEnvelope command, IReadOnlyList<ProjectionEventDto> sourceEvents);
}
