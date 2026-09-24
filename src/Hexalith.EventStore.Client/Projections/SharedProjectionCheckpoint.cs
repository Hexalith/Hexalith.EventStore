namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable source position and opaque consumer checkpoint accepted with one journal entry.</summary>
public sealed record SharedProjectionCheckpoint(long Position, byte[] Token);
