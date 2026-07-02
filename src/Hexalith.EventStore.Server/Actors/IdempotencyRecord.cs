namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Storage-optimized DTO for idempotency state in IActorStateManager.
/// Intentionally separate from <see cref="CommandProcessingResult"/> to allow independent evolution.
/// </summary>
/// <param name="CausationId">The causation identifier (idempotency key).</param>
/// <param name="CorrelationId">The correlation identifier for self-contained result reconstruction.</param>
/// <param name="Accepted">Whether the command was accepted.</param>
/// <param name="ErrorMessage">Optional error message if rejected.</param>
/// <param name="ProcessedAt">When the command was processed.</param>
/// <param name="EventCount">The number of events persisted by the original command.</param>
/// <param name="ResultPayload">Optional serialized result payload from the original command.</param>
/// <param name="BackpressureExceeded">Whether the original result was rejected by backpressure.</param>
/// <param name="BackpressurePendingCount">The original observed pending command count.</param>
/// <param name="BackpressureThreshold">The original configured backpressure threshold.</param>
public record IdempotencyRecord(
    string CausationId,
    string? CorrelationId,
    bool Accepted,
    string? ErrorMessage,
    DateTimeOffset ProcessedAt,
    int EventCount = 0,
    string? ResultPayload = null,
    bool BackpressureExceeded = false,
    int? BackpressurePendingCount = null,
    int? BackpressureThreshold = null) {
    /// <summary>
    /// Creates an <see cref="IdempotencyRecord"/> from a <see cref="CommandProcessingResult"/>.
    /// </summary>
    /// <param name="causationId">The causation identifier.</param>
    /// <param name="result">The command processing result.</param>
    /// <returns>A new idempotency record.</returns>
    public static IdempotencyRecord FromResult(string causationId, CommandProcessingResult result) {
        ArgumentException.ThrowIfNullOrWhiteSpace(causationId);
        ArgumentNullException.ThrowIfNull(result);

        return new IdempotencyRecord(
            causationId,
            result.CorrelationId,
            result.Accepted,
            result.ErrorMessage,
            DateTimeOffset.UtcNow,
            result.EventCount,
            result.ResultPayload,
            result.BackpressureExceeded,
            result.BackpressurePendingCount,
            result.BackpressureThreshold);
    }

    /// <summary>
    /// Reconstructs a <see cref="CommandProcessingResult"/> from the stored record.
    /// </summary>
    /// <returns>The reconstructed command processing result.</returns>
    public CommandProcessingResult ToResult()
        => new(
            Accepted,
            ErrorMessage,
            CorrelationId,
            EventCount,
            ResultPayload,
            BackpressureExceeded,
            BackpressurePendingCount,
            BackpressureThreshold);
}
