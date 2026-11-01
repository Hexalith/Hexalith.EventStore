using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Full submitted identity and observed transport outcome for the bound two-key experiment.</summary>
/// <param name="Request">The exact submitted request.</param>
/// <param name="ActorId">The exact routed actor.</param>
/// <param name="Outcome">Pending, acknowledged success, known rejection, or uncertain.</param>
/// <param name="ReplyCode">The acknowledged actor result code.</param>
/// <param name="Sends">Actual SDK sends.</param>
/// <param name="FaultMarker">The exact selected provider marker, when faulted.</param>
/// <param name="FaultBoundary">The explicitly armed provider boundary.</param>
/// <param name="OneKeyBoundary">The ordinary implicit save boundary, when selected.</param>
/// <param name="FaultVerified">Whether final settled fault evidence passed before instrumentation removal.</param>
internal sealed record RegisteredShardOperation(ShardRequest Request, string ActorId, string Outcome = "pending",
    string? ReplyCode = null, int Sends = 0, string? FaultMarker = null,
    PostgresCommitBoundary? FaultBoundary = null, bool FaultVerified = false, PostgresOneKeyBoundary? OneKeyBoundary = null);
