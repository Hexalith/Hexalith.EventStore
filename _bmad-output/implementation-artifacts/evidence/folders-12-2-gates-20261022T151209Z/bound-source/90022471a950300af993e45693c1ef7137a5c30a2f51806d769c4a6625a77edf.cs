namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Full result of a consistency check including anomaly details.
/// </summary>
/// <param name="CheckId">Unique identifier for the consistency check.</param>
/// <param name="Status">Current status of the check.</param>
/// <param name="TenantId">Tenant scope, or null for all tenants.</param>
/// <param name="Domain">Domain scope, or null for all domains.</param>
/// <param name="CheckTypes">Types of consistency checks included in this run.</param>
/// <param name="StartedAtUtc">When the check was triggered.</param>
/// <param name="CompletedAtUtc">When the check finished, or null if still running.</param>
/// <param name="TimeoutUtc">Deadline after which a running check is considered timed out.</param>
/// <param name="StreamsChecked">Number of aggregate streams verified.</param>
/// <param name="AnomaliesFound">Total number of anomalies discovered (may exceed Anomalies.Count when truncated).</param>
/// <param name="Anomalies">Discovered anomalies, sorted by severity (Critical first). Capped at 500.</param>
/// <param name="Truncated">Whether the anomaly list was truncated due to exceeding the 500 cap.</param>
/// <param name="ErrorMessage">Error details when status is Failed, otherwise null.</param>
public record ConsistencyCheckResult(
    string CheckId,
    ConsistencyCheckStatus Status,
    string? TenantId,
    string? Domain,
    IReadOnlyList<ConsistencyCheckType> CheckTypes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset TimeoutUtc,
    int StreamsChecked,
    int AnomaliesFound,
    IReadOnlyList<ConsistencyAnomaly> Anomalies,
    bool Truncated,
    string? ErrorMessage);
