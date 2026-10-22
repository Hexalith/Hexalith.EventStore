namespace Hexalith.EventStore.Client.Projections;

/// <summary>A generation-selected read with an explicit stale/unavailable signal.</summary>
public sealed record SharedProjectionReadResult<TValue>(TValue? Value, long Generation, bool IsStale, bool IsAvailable)
    where TValue : class
{
    /// <summary>Whether unresolved delivery identity evidence prevents current readiness.</summary>
    public bool IsDegraded { get; init; }

    /// <summary>Gets the ETag of the selected physical value, when one was read.</summary>
    public string? SelectedETag { get; init; }
}
