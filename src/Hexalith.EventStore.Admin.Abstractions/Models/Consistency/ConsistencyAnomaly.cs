namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Represents a single anomaly discovered during a consistency check.
/// </summary>
/// <param name="AnomalyId">Unique identifier for this anomaly.</param>
/// <param name="CheckType">The type of consistency check that discovered the anomaly.</param>
/// <param name="Severity">Severity level of the anomaly.</param>
/// <param name="TenantId">Tenant identifier where the anomaly was found.</param>
/// <param name="Domain">Domain where the anomaly was found.</param>
/// <param name="AggregateId">Aggregate identifier where the anomaly was found.</param>
/// <param name="Description">Human-readable description of the anomaly.</param>
/// <param name="Details">Optional detailed information (e.g., raw state data).</param>
/// <param name="ExpectedSequence">Expected sequence number, when applicable.</param>
/// <param name="ActualSequence">Actual sequence number found, when applicable.</param>
public record ConsistencyAnomaly(
    string AnomalyId,
    ConsistencyCheckType CheckType,
    AnomalySeverity Severity,
    string TenantId,
    string Domain,
    string AggregateId,
    string Description,
    string? Details,
    long? ExpectedSequence,
    long? ActualSequence);
