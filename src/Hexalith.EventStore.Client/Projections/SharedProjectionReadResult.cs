namespace Hexalith.EventStore.Client.Projections;

/// <summary>A generation-selected read with an explicit stale/unavailable signal.</summary>
public sealed record SharedProjectionReadResult<TValue>(TValue? Value, long Generation, bool IsStale, bool IsAvailable)
    where TValue : class;
