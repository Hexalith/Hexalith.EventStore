namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public freshness policy supplied with a gateway query request.
/// </summary>
/// <param name="RequireFresh">Whether the caller requires a fresh projection response. Freshness enforcement is reserved and currently rejected when true.</param>
/// <param name="MaxStaleness">Optional maximum accepted projection staleness. Reserved until projection freshness metadata is available.</param>
public sealed record QueryFreshnessPolicy(bool? RequireFresh = null, TimeSpan? MaxStaleness = null);
