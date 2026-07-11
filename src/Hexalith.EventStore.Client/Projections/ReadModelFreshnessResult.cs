namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// A freshness-aware read of a persisted read model: the value, its ETag, and its classified freshness.
/// </summary>
/// <typeparam name="TValue">The read-model type.</typeparam>
/// <param name="Value">The persisted value, or <see langword="null"/> when the key is absent.</param>
/// <param name="ETag">The ETag of the read, or <see langword="null"/> when the key is absent.</param>
/// <param name="Freshness">The classified freshness state of the read model.</param>
public sealed record ReadModelFreshnessResult<TValue>(
    TValue? Value,
    string? ETag,
    ReadModelFreshnessState Freshness)
    where TValue : class, IReadModelFreshness;
