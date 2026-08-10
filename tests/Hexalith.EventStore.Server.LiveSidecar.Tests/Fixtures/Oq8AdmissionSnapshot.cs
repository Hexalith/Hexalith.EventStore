using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Contains a support-safe projection of durable admission state.</summary>
/// <param name="Exists">Whether any admission authority exists.</param>
/// <param name="State">The live or tombstone state.</param>
/// <param name="FencingToken">The live fence, or zero for a tombstone.</param>
/// <param name="HasIntent">Whether live canonical intent is retained.</param>
/// <param name="HasReplay">Whether an exact replay result is retained.</param>
/// <param name="ReplaySha256">The replay-result digest, when present.</param>
/// <param name="ReplayExpiresAt">The inclusive replay expiry, when present.</param>
/// <param name="HasExecutionIdentity">Whether live execution identities are retained.</param>
/// <param name="IsMinimalTombstone">Whether only the approved fence-free tombstone shape remains.</param>
internal sealed record Oq8AdmissionSnapshot(
    bool Exists,
    IdempotencyAdmissionState? State,
    long FencingToken,
    bool HasIntent,
    bool HasReplay,
    string? ReplaySha256,
    DateTimeOffset? ReplayExpiresAt,
    bool HasExecutionIdentity,
    bool IsMinimalTombstone);
