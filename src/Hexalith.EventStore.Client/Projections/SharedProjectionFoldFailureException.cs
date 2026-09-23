namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Signals an expected, retryable fold failure. The coordinator retains the delivery and increments
/// its durable failure count. At the limit it prepares the supplied parking mutations before writing them.
/// </summary>
public sealed class SharedProjectionFoldFailureException : Exception
{
    private readonly Func<int, IReadOnlyList<ReadModelBatchOperation>> _createParkingMutations;

    /// <summary>Creates a failure for one source position within the journaled source stream.</summary>
    public SharedProjectionFoldFailureException(
        string sourceStream,
        long sourcePosition,
        int retryLimit,
        Func<int, IReadOnlyList<ReadModelBatchOperation>> createParkingMutations)
        : base("The shared projection fold cannot process a source position.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceStream);
        ArgumentOutOfRangeException.ThrowIfNegative(sourcePosition);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryLimit, 1);
        SourceStream = sourceStream;
        SourcePosition = sourcePosition;
        RetryLimit = retryLimit;
        _createParkingMutations = createParkingMutations ?? throw new ArgumentNullException(nameof(createParkingMutations));
    }

    /// <summary>Gets the source stream containing the failed position.</summary>
    public string SourceStream { get; }

    /// <summary>Gets the failed source position within that stream.</summary>
    public long SourcePosition { get; }

    /// <summary>Gets the number of consecutive failed attempts that causes terminal parking.</summary>
    public int RetryLimit { get; }

    internal IReadOnlyList<ReadModelBatchOperation> CreateParkingMutations(int failureCount)
        => _createParkingMutations(failureCount);
}
