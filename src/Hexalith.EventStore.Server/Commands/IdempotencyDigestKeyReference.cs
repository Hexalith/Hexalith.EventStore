namespace Hexalith.EventStore.Server.Commands;

/// <summary>Counts durable references to one digest-key version.</summary>
/// <param name="DigestKeyVersion">The referenced digest-key version.</param>
/// <param name="Kind">The durable reference category.</param>
/// <param name="Count">The number of retained references.</param>
public sealed record IdempotencyDigestKeyReference(
    string DigestKeyVersion,
    IdempotencyDigestKeyReferenceKind Kind,
    long Count);
