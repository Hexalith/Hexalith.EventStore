namespace Hexalith.EventStore.Contracts.Replay;

/// <summary>Opaque successor handoff returned by one async paged replay call.</summary>
/// <remarks>Only a trusted operation ledger can validate or commit this progress.</remarks>
public sealed record PagedProgress {
    private const int MaximumHandleBytes = 2 * 1024 * 1024;
    private readonly byte[] _successorHandleToken;
    private readonly byte[] _storedAccumulator;
    private readonly byte[] _effectiveAccumulator;

    /// <summary>Snapshots the opaque successor and exact resulting accumulators.</summary>
    public PagedProgress(
        byte[] successorHandleToken,
        byte[] storedAccumulator,
        byte[] effectiveAccumulator,
        long processedStart,
        long processedEnd,
        int processedCount,
        bool isFinalState) {
        ArgumentNullException.ThrowIfNull(successorHandleToken);
        ArgumentNullException.ThrowIfNull(storedAccumulator);
        ArgumentNullException.ThrowIfNull(effectiveAccumulator);
        if (successorHandleToken.Length is 0 or > MaximumHandleBytes) {
            throw new ArgumentOutOfRangeException(nameof(successorHandleToken));
        }

        if (storedAccumulator.Length != 32 || effectiveAccumulator.Length != 32) {
            throw new ArgumentException("Replay accumulators must contain exactly 32 bytes.");
        }

        if (processedCount is < 0 or > 256) {
            throw new ArgumentOutOfRangeException(nameof(processedCount));
        }

        _successorHandleToken = successorHandleToken.ToArray();
        _storedAccumulator = storedAccumulator.ToArray();
        _effectiveAccumulator = effectiveAccumulator.ToArray();
        ProcessedStart = processedStart;
        ProcessedEnd = processedEnd;
        ProcessedCount = processedCount;
        IsFinalState = isFinalState;
    }

    /// <summary>Gets a copy of the opaque successor-state handle.</summary>
    public byte[] SuccessorHandleToken => _successorHandleToken.ToArray();

    /// <summary>Gets a copy of the resulting stored-source accumulator.</summary>
    public byte[] StoredAccumulator => _storedAccumulator.ToArray();

    /// <summary>Gets a copy of the resulting effective-payload accumulator.</summary>
    public byte[] EffectiveAccumulator => _effectiveAccumulator.ToArray();

    /// <summary>Gets the first processed sequence.</summary>
    public long ProcessedStart { get; }

    /// <summary>Gets the last processed sequence.</summary>
    public long ProcessedEnd { get; }

    /// <summary>Gets the processed event count.</summary>
    public int ProcessedCount { get; }

    /// <summary>Gets whether the successor represents the final target state.</summary>
    public bool IsFinalState { get; }
}
