namespace Hexalith.EventStore.Client.Events;
/// <summary>Represents untrusted distinct anchor-selection fields; actual private candidate intake supplies authority.</summary>
/// <param name = "TenantId">The exact addressed tenant.</param>
/// <param name = "Domain">The exact addressed domain.</param>
/// <param name = "AggregateId">The exact addressed aggregate identifier.</param>
/// <param name = "AggregateType">The pinned aggregate contract.</param>
/// <param name = "SourceBindingHash">The original requested fixed-source binding hash.</param>
/// <param name = "RegistryFingerprint">The current exact registry fingerprint.</param>
/// <param name = "ReconstructionBindingHash">The exact state, serializer and Apply binding.</param>
/// <param name = "SnapshotWitnessHash">The exact privately verified snapshot witness image hash.</param>
/// <param name = "CanonicalStateHash">The canonical initial state hash.</param>
/// <param name = "CoveredAccumulator">The completed origin's covered logical accumulator.</param>
/// <param name = "CoveredEffectiveChain">The completed origin's full effective history commitment.</param>
/// <param name = "CoveredTranscript">The completed origin's exact page transcript commitment.</param>
/// <param name = "CoveredSequence">The positive witnessed snapshot sequence.</param>
/// <param name = "ActorHead">The fixed actual source head.</param>
/// <param name = "TargetSequence">The original requested target.</param>
/// <param name = "ModelId">The explicit distinct anchored preparation model.</param>
internal sealed record DaprLogicalReplayAnchorSelection(string TenantId, string Domain, string AggregateId, string AggregateType, ReadOnlyMemory<byte> SourceBindingHash, ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> ReconstructionBindingHash, ReadOnlyMemory<byte> SnapshotWitnessHash, ReadOnlyMemory<byte> CanonicalStateHash, ReadOnlyMemory<byte> CoveredAccumulator, ReadOnlyMemory<byte> CoveredEffectiveChain, ReadOnlyMemory<byte> CoveredTranscript, long CoveredSequence, long ActorHead, long TargetSequence, string ModelId);
