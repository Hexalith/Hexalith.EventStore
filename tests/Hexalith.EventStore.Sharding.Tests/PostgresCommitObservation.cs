namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>One observed frontend COMMIT and its exact backend completion, including provider-only replays.</summary>
/// <param name="Id">The proxy-wide execution identity.</param>
/// <param name="ConnectionId">The accepted protocol session.</param>
/// <param name="Marker">The exact selected write marker, when present.</param>
/// <param name="Forwarded">Whether the complete COMMIT frame was forwarded.</param>
/// <param name="Completed">Whether PostgreSQL reported COMMIT completion.</param>
/// <param name="AcknowledgementWithheld">Whether its successful completion was gated.</param>
internal sealed record PostgresCommitObservation(long Id, int ConnectionId, string? Marker,
    bool Forwarded = false, bool Completed = false, bool AcknowledgementWithheld = false);
