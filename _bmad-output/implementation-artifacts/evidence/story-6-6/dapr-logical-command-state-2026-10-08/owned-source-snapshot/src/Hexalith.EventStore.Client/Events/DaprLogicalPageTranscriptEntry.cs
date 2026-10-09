namespace Hexalith.EventStore.Client.Events;

/// <summary>Contains the exact nonrecursive scalar fields of an operation-owned page transcript entry.</summary>
/// <param name="PageOrdinal">Committed page ordinal.</param>
/// <param name="Generation">Committed owner generation.</param>
/// <param name="RequestHash">Digest of the exact page request.</param>
/// <param name="PreviousAccumulator">Prior complete logical accumulator.</param>
/// <param name="Accumulator">Successor complete logical accumulator.</param>
/// <param name="StartSequence">Contiguous page start.</param>
/// <param name="EndSequence">Contiguous page end, zero only for an empty target.</param>
/// <param name="Count">Admitted effective-event count.</param>
/// <param name="ResponseHash">Digest of exact retained response bytes.</param>
/// <param name="IsFinal">Whether this page completes the fixed target.</param>
/// <param name="PriorStateHash">Optional predecessor canonical state digest.</param>
/// <param name="CanonicalStateHash">Optional successor canonical state digest.</param>
/// <param name="PreviousEffectiveChain">Effective-event chain before this page.</param>
/// <param name="EffectiveChain">Effective-event chain after this page.</param>
internal sealed record DaprLogicalPageTranscriptEntry(long PageOrdinal, long Generation, ReadOnlyMemory<byte> RequestHash,
    ReadOnlyMemory<byte> PreviousAccumulator, ReadOnlyMemory<byte> Accumulator, long StartSequence, long EndSequence,
    int Count, ReadOnlyMemory<byte> ResponseHash, bool IsFinal, ReadOnlyMemory<byte>? PriorStateHash,
    ReadOnlyMemory<byte>? CanonicalStateHash, ReadOnlyMemory<byte> PreviousEffectiveChain, ReadOnlyMemory<byte> EffectiveChain);
