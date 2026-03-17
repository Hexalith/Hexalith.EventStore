namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Thrown when an aggregate's in-flight command count exceeds the configured backpressure threshold (FR67).
/// Properties are for server-side structured logging only — they must NEVER appear in the client-facing 429 response
/// (UX-DR10, Rule E6). This exception does NOT need serialization attributes since it is never serialized across
/// DAPR boundaries (thrown from SubmitCommandHandler inside the MediatR pipeline, not from the actor).
/// </summary>
public class BackpressureExceededException : Exception {
    private const string DefaultDetailTemplate =
        "Backpressure threshold exceeded for aggregate actor '{0}'. " +
        "Current depth: {1}. The command was rejected before entering the pipeline.";

    /// <summary>Standard parameterless constructor.</summary>
    public BackpressureExceededException()
        : base("Backpressure threshold exceeded.") {
        AggregateActorId = string.Empty;
        CorrelationId = string.Empty;
    }

    /// <summary>Standard message-only constructor.</summary>
    public BackpressureExceededException(string message)
        : base(message) {
        AggregateActorId = string.Empty;
        CorrelationId = string.Empty;
    }

    /// <summary>Standard message+inner constructor.</summary>
    public BackpressureExceededException(string message, Exception innerException)
        : base(message, innerException) {
        AggregateActorId = string.Empty;
        CorrelationId = string.Empty;
    }

    /// <summary>Primary domain constructor with full context for structured logging.</summary>
    public BackpressureExceededException(
        string aggregateActorId,
        string? tenantId,
        string correlationId,
        int currentDepth)
        : base(string.Format(DefaultDetailTemplate, aggregateActorId, currentDepth)) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        AggregateActorId = aggregateActorId;
        TenantId = tenantId;
        CorrelationId = correlationId;
        CurrentDepth = currentDepth;
    }

    /// <summary>Gets the canonical actor ID of the aggregate that exceeded backpressure.</summary>
    public string AggregateActorId { get; }

    /// <summary>Gets the tenant ID associated with the rejected command.</summary>
    public string? TenantId { get; }

    /// <summary>Gets the correlation ID of the rejected command.</summary>
    public string CorrelationId { get; }

    /// <summary>Gets the current in-flight command depth at the time of rejection.</summary>
    public int CurrentDepth { get; }
}
