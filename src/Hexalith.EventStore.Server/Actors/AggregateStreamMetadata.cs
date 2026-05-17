namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Describes whether an aggregate stream exists and its current highest sequence.
/// </summary>
/// <param name="Exists">Indicates whether aggregate metadata exists for the stream.</param>
/// <param name="CurrentSequence">The current highest sequence number, or zero when no events have been stored.</param>
public readonly record struct AggregateStreamMetadata(bool Exists, long CurrentSequence);
