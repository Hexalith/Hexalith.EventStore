namespace Hexalith.EventStore.DomainService;

/// <summary>One durable, bounded part of an authoritative shared rebuild Begin inventory.</summary>
/// <param name="Ordinal">The part ordinal within the capture.</param>
/// <param name="Heads">Captured source positions by owner.</param>
/// <param name="ErasedOwners">Owners whose completed erasure is captured in this part.</param>
/// <param name="Digest">The exact canonical part content digest used for retry admission.</param>
public sealed record DomainSharedProjectionBeginInventoryPage(
    int Ordinal, IReadOnlyDictionary<string, long> Heads, IReadOnlyList<string> ErasedOwners, string Digest);
