namespace Hexalith.EventStore.Server.Commands;

/// <summary>Tracks one protected admission actor decision and fence.</summary>
/// <param name="ActorId">The protected tenant/key actor identifier.</param>
/// <param name="FencingToken">The active fence.</param>
/// <param name="Decision">The admission decision.</param>
/// <param name="ReplayResult">The optional replay result.</param>
/// <param name="ExecutionMessageId">An opaque per-execution downstream message identifier unrelated to protected key material.</param>
public sealed record IdempotencyAdmissionSession(
    string ActorId,
    long FencingToken,
    Actors.IdempotencyAdmissionDecision Decision,
    Actors.CommandProcessingResult? ReplayResult = null,
    string? ExecutionMessageId = null);
