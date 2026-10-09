namespace Hexalith.EventStore.Client.Events;
/// <summary>Contains unverified fields for the separately selected anchored prefix schema.</summary>
/// <param name = "Prefix">The carried contiguous logical tail and requested source.</param>
/// <param name = "SelectionHash">The exact private anchor selection image digest.</param>
/// <param name = "CoveredSequence">The positive canonical snapshot sequence.</param>
/// <param name = "InitialStateHash">The exact initial canonical state digest.</param>
internal sealed record DaprLogicalAnchoredPrefixClaim(DaprLogicalPrefixClaim Prefix, ReadOnlyMemory<byte> SelectionHash, long CoveredSequence, ReadOnlyMemory<byte> InitialStateHash);
