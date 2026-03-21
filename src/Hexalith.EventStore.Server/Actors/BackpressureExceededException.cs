namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Exception raised when a command is rejected due to per-aggregate backpressure (Story 4.3, FR67).
/// The aggregate's pending command count exceeds the configured threshold.
/// </summary>
public class BackpressureExceededException : InvalidOperationException {
    public BackpressureExceededException(
        string correlationId,
        string tenantId,
        string domain,
        string aggregateId,
        int pendingCount,
        int threshold)
        : base($"Backpressure exceeded for aggregate {domain}:{aggregateId} (tenant: {tenantId}): {pendingCount} pending commands (threshold: {threshold})") {
        CorrelationId = correlationId;
        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
        PendingCount = pendingCount;
        Threshold = threshold;
    }

    public string CorrelationId { get; }

    public string TenantId { get; }

    public string Domain { get; }

    public string AggregateId { get; }

    public int PendingCount { get; }

    public int Threshold { get; }
}
