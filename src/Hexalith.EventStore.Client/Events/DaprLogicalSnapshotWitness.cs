namespace Hexalith.EventStore.Client.Events;
/// <summary>Contains untrusted distinct snapshot fields; actual paired and completed-origin intake supplies authority.</summary>
/// <param name = "TenantId">The canonical tenant.</param>
/// <param name = "Domain">The canonical domain.</param>
/// <param name = "AggregateId">The addressed aggregate.</param>
/// <param name = "AggregateType">The supplied aggregate route.</param>
/// <param name = "SourceBindingHash">The fixed source targeted at the covered sequence.</param>
/// <param name = "CoveredSequence">The positive covered source sequence.</param>
/// <param name = "StorageKey">The exact application snapshot key.</param>
/// <param name = "WitnessKey">The exact paired witness key.</param>
/// <param name = "StorageHash">The exact stored byte hash.</param>
/// <param name = "FoldedHash">The canonical readable state hash.</param>
/// <param name = "RegistryFingerprint">The admitted current registry.</param>
/// <param name = "ReconstructionBindingHash">The exact supplied canonical codec and Apply binding.</param>
/// <param name = "Accumulator">The completed covered logical accumulator.</param>
/// <param name = "OperationId">The completed operation identity.</param>
/// <param name = "Generation">The positive completed owner generation.</param>
/// <param name = "TranscriptHash">The completed exact page transcript.</param>
/// <param name = "EffectiveChainHash">The completed effective-event chain.</param>
/// <param name = "ProtectionCodec">The explicitly supported plaintext codec.</param>
/// <param name = "ModelId">The distinct selected snapshot model.</param>
/// <param name = "SerializerId">The exact supplied state serializer.</param>
internal sealed record DaprLogicalSnapshotWitness(string TenantId, string Domain, string AggregateId, string AggregateType, ReadOnlyMemory<byte> SourceBindingHash, long CoveredSequence, string StorageKey, string WitnessKey, ReadOnlyMemory<byte> StorageHash, ReadOnlyMemory<byte> FoldedHash, ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> ReconstructionBindingHash, ReadOnlyMemory<byte> Accumulator, string OperationId, long Generation, ReadOnlyMemory<byte> TranscriptHash, ReadOnlyMemory<byte> EffectiveChainHash, string ProtectionCodec, string ModelId, string SerializerId);
