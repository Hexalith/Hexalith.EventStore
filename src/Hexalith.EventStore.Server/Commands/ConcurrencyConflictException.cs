namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Thrown when an optimistic concurrency conflict is detected during aggregate event persistence.
/// The actor processing layer (Story 3.7) throws this when an ETag mismatch occurs on the
/// aggregate metadata key, indicating another command was processed for the same aggregate
/// between state read and event write.
/// </summary>
public class ConcurrencyConflictException : Exception {
    private const string DefaultDetailTemplate =
        "An optimistic concurrency conflict occurred on entity '{0}'. " +
        "Another command was processed concurrently. " +
        "Retry the command to process against the updated state.";

    /// <summary>Standard parameterless constructor (serialization support).</summary>
    public ConcurrencyConflictException()
        : base("An optimistic concurrency conflict occurred.") {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Standard message-only constructor (serialization support).</summary>
    public ConcurrencyConflictException(string message)
        : base(message) {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Standard message+inner constructor (serialization support).</summary>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException) {
        CorrelationId = string.Empty;
        AggregateId = string.Empty;
    }

    /// <summary>Primary domain constructor with full context.</summary>
    public ConcurrencyConflictException(
        string correlationId,
        string aggregateId,
        string? tenantId = null,
        string? detail = null,
        string? conflictSource = null,
        Exception? innerException = null)
        : base(detail ?? string.Format(DefaultDetailTemplate, aggregateId), innerException) {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        CorrelationId = correlationId;
        AggregateId = aggregateId;
        TenantId = tenantId;
        ConflictSource = conflictSource;
    }

    public string CorrelationId { get; }

    public string AggregateId { get; }

    public string? TenantId { get; }

    /// <summary>
    /// Optional identifier for the source of the conflict (e.g., "StateStore", "ActorReentrancy").
    /// Provides future extensibility for distinguishing conflict origins without breaking changes.
    /// Reserved for Epic 3 (Story 3.7) when ETag-based concurrency is implemented. Currently unused in v1.
    /// </summary>
    public string? ConflictSource { get; }
}
