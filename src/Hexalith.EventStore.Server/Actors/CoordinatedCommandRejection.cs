namespace Hexalith.EventStore.Server.Actors;

/// <summary>Durable, payload-free receipt for an authoritative pre-dispatch conflict.</summary>
/// <param name="CommandDigest">The exact rejected command identity and payload digest.</param>
/// <param name="Result">The stable rejection returned on an exact retry.</param>
internal sealed record CoordinatedCommandRejection(string CommandDigest, CommandProcessingResult Result);
