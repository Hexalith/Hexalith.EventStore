namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable source position and opaque consumer checkpoint accepted with one journal entry.</summary>
/// <param name="Position">The accepted source position.</param>
/// <param name="Token">The opaque source checkpoint token.</param>
/// <param name="OwnerSequenceNumber">The accepted aggregate event sequence, or zero for a legacy checkpoint.</param>
/// <param name="CapturedEventDigests">SHA-256 identities of rebuilt owner events in ordinal order, when captured.</param>
public sealed record SharedProjectionCheckpoint(long Position, byte[] Token, long OwnerSequenceNumber = 0,
    byte[]? CapturedEventDigests = null);
