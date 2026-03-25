namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Summary of a consistency check for list display (without full anomaly details).
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
/// <param name="AnomaliesFound">Total number of anomalies discovered.</param>
public record ConsistencyCheckSummary(
    string CheckId,
    ConsistencyCheckStatus Status,
    string? TenantId,
    string? Domain,
    IReadOnlyList<ConsistencyCheckType> CheckTypes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset TimeoutUtc,
    int StreamsChecked,
    int AnomaliesFound);
