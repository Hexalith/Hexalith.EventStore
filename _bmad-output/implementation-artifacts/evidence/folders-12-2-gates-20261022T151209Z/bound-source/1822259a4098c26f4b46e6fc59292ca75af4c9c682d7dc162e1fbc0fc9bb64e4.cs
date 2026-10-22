namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Wire view of the existing metadata-only actor method; zero status means native Available.</summary>
/// <param name="Status">The native inspection status, without reclassifying Unsupported or Invalid.</param>
/// <param name="Exists">Whether source metadata exists.</param>
/// <param name="CurrentSequence">The native committed head.</param>
/// <param name="Erased">Whether native erasure evidence is present.</param>
/// <param name="Proof">The native validated proof, when available.</param>
public sealed record NativeSourceInspection(int Status, bool Exists, long CurrentSequence, bool Erased, NativeSourceProof? Proof);
