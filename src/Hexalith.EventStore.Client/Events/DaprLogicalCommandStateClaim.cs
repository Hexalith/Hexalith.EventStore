namespace Hexalith.EventStore.Client.Events;

/// <summary>Contains a distinct logical purpose-07 completed command-state claim, without historical provider authority.</summary>
/// <param name="TenantId">Canonical tenant.</param>
/// <param name="Domain">Exact command domain.</param>
/// <param name="AggregateId">Addressed aggregate identifier.</param>
/// <param name="AggregateType">Pinned aggregate type.</param>
/// <param name="CommandType">Exact command type.</param>
/// <param name="MessageId">Exact command MessageId.</param>
/// <param name="CommandHash">Digest of every private command field.</param>
/// <param name="SourceBindingHash">Digest of the immutable addressed source.</param>
/// <param name="ActorHead">Immutable actor head.</param>
/// <param name="TargetSequence">Fixed replay target.</param>
/// <param name="RegistryFingerprint">Current sealed registry fingerprint.</param>
/// <param name="ReconstructionBindingHash">Exact canonical reconstruction binding.</param>
/// <param name="OperationId">Stable operation identifier.</param>
/// <param name="OwnerId">Terminal actor owner identifier.</param>
/// <param name="Generation">Committed owner generation.</param>
/// <param name="PageOrdinal">Terminal committed page ordinal.</param>
/// <param name="CompletedSequence">Complete replay sequence, equal to target.</param>
/// <param name="Accumulator">Complete logical prefix accumulator.</param>
/// <param name="FinalPrefixHash">Digest of the exact final signed prefix claim.</param>
/// <param name="FinalSourceProofHash">Digest of the exact final source proof frame.</param>
/// <param name="FinalResponseHash">Digest of the retained final response bytes.</param>
/// <param name="CanonicalStateHash">Digest of the exact privately retained canonical state.</param>
/// <param name="EffectiveChainHash">Cumulative verified effective-event chain.</param>
/// <param name="TranscriptHash">Exact committed page transcript from genesis.</param>
/// <param name="IssuedAt">Current logical signing time.</param>
/// <param name="ExpiresAt">Bounded proof expiry under the current key.</param>
internal sealed record DaprLogicalCommandStateClaim(string TenantId, string Domain, string AggregateId, string AggregateType,
    string CommandType, string MessageId, ReadOnlyMemory<byte> CommandHash, ReadOnlyMemory<byte> SourceBindingHash,
    long ActorHead, long TargetSequence, ReadOnlyMemory<byte> RegistryFingerprint, ReadOnlyMemory<byte> ReconstructionBindingHash,
    string OperationId, string OwnerId, long Generation, long PageOrdinal, long CompletedSequence,
    ReadOnlyMemory<byte> Accumulator, ReadOnlyMemory<byte> FinalPrefixHash, ReadOnlyMemory<byte> FinalSourceProofHash,
    ReadOnlyMemory<byte> FinalResponseHash, ReadOnlyMemory<byte> CanonicalStateHash, ReadOnlyMemory<byte> EffectiveChainHash,
    ReadOnlyMemory<byte> TranscriptHash, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
