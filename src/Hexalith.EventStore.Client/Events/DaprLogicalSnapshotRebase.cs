namespace Hexalith.EventStore.Client.Events;
/// <summary>Contains untrusted rebase candidate framing; equality requires separate actual-owner qualification.</summary>
/// <param name = "PriorWitnessHash">The exact prior witness hash.</param>
/// <param name = "SuccessorWitnessHash">The exact successor witness hash.</param>
/// <param name = "PriorSourceHash">The prior fixed source.</param>
/// <param name = "SuccessorSourceHash">The successor fixed source.</param>
/// <param name = "PriorRegistry">The prior registry.</param>
/// <param name = "SuccessorRegistry">The successor registry.</param>
/// <param name = "FoldedHash">The proposed equal canonical state hash.</param>
/// <param name = "ModelId">The distinct rebase model.</param>
internal sealed record DaprLogicalSnapshotRebase(ReadOnlyMemory<byte> PriorWitnessHash, ReadOnlyMemory<byte> SuccessorWitnessHash, ReadOnlyMemory<byte> PriorSourceHash, ReadOnlyMemory<byte> SuccessorSourceHash, ReadOnlyMemory<byte> PriorRegistry, ReadOnlyMemory<byte> SuccessorRegistry, ReadOnlyMemory<byte> FoldedHash, string ModelId);
