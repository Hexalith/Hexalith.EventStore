namespace Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

/// <summary>
/// Severity level of a consistency anomaly.
/// </summary>
public enum AnomalySeverity {
    /// <summary>A potential issue that may not indicate data corruption (e.g., missing snapshot for large aggregate).</summary>
    Warning,

    /// <summary>A confirmed data inconsistency (e.g., sequence gap, metadata mismatch).</summary>
    Error,

    /// <summary>A severe integrity violation (e.g., first event missing, projection ahead of event stream).</summary>
    Critical,
}
