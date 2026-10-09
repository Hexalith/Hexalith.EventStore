namespace Hexalith.EventStore.Client.Events;
/// <summary>Contains untrusted projection checkpoint candidate bytes; aggregate snapshot admission never consumes them.</summary>
/// <param name = "TenantId">The canonical tenant.</param>
/// <param name = "Domain">The canonical domain.</param>
/// <param name = "Projection">The exact projection identity.</param>
/// <param name = "ScopeHash">The exact key-space scope.</param>
/// <param name = "CoveredSequence">The nonnegative covered sequence.</param>
/// <param name = "Accumulator">The covered logical accumulator.</param>
/// <param name = "RegistryFingerprint">The current registry.</param>
/// <param name = "RootHash">The named logical root hash.</param>
/// <param name = "ModelId">The distinct checkpoint model.</param>
internal sealed record DaprLogicalCheckpointWitness(string TenantId, string Domain, string Projection, ReadOnlyMemory<byte> ScopeHash, long CoveredSequence, ReadOnlyMemory<byte> Accumulator, ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> RootHash, string ModelId);
