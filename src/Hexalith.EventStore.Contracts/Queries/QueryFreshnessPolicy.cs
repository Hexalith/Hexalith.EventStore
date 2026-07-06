namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public freshness policy supplied with a gateway query request.
/// </summary>
/// <param name="RequireFresh">Whether the caller requires a fresh projection response.</param>
/// <param name="MaxStaleness">Optional maximum accepted projection staleness.</param>
public sealed record QueryFreshnessPolicy(bool? RequireFresh = null, TimeSpan? MaxStaleness = null);
