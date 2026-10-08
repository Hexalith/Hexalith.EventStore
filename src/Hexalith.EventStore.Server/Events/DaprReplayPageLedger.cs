namespace Hexalith.EventStore.Server.Events;

/// <summary>Records the exact idempotent page request, predecessor and pinned response in one actor save.</summary>
/// <param name="PageOrdinal">The contiguous page ordinal.</param>
/// <param name="Generation">The admitted execution generation.</param>
/// <param name="RequestHash">The exact request identity hash.</param>
/// <param name="PreviousAccumulator">The operation-owned predecessor accumulator.</param>
/// <param name="Accumulator">The admitted complete successor accumulator.</param>
/// <param name="StartSequence">The page's first source sequence.</param>
/// <param name="EndSequence">The page's last source sequence.</param>
/// <param name="Count">The number of contiguous source events.</param>
/// <param name="ResponseHash">The exact pinned response digest.</param>
/// <param name="IsFinal">Whether this page atomically carries the final result.</param>
internal sealed record DaprReplayPageLedger(long PageOrdinal, long Generation, byte[] RequestHash,
    byte[] PreviousAccumulator, byte[] Accumulator, long StartSequence, long EndSequence, int Count,
    byte[] ResponseHash, bool IsFinal)
{
    /// <summary>Gets the immutable canonical predecessor digest for reconstruction.</summary>
    public byte[]? PriorStateHash { get; init; }
    /// <summary>Gets the canonical successor persisted with this response and ledger.</summary>
    public byte[]? CanonicalStateHash { get; init; }
}
