namespace Hexalith.EventStore.Client.Projections;

/// <summary>A complete projection snapshot exceeded an explicitly supported budget.</summary>
/// <param name="limit">The budget name.</param>
/// <param name="actual">The observed lower bound on snapshot cost.</param>
/// <param name="maximum">The supported maximum.</param>
public sealed class ProjectionSnapshotLimitExceededException(string limit, long actual, long maximum)
    : InvalidOperationException($"Projection snapshot {limit} budget exceeded: {actual} > {maximum}.")
{
    /// <summary>Gets the exceeded budget name.</summary>
    public string Limit { get; } = limit;

    /// <summary>Gets the observed cost, which can be a lower bound for streamed JSON.</summary>
    public long Actual { get; } = actual;

    /// <summary>Gets the supported maximum.</summary>
    public long Maximum { get; } = maximum;
}
