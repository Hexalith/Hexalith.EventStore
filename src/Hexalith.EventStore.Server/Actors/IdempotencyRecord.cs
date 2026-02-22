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
public record IdempotencyRecord(
    string CausationId,
    string? CorrelationId,
    bool Accepted,
    string? ErrorMessage,
    DateTimeOffset ProcessedAt) {
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
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Reconstructs a <see cref="CommandProcessingResult"/> from the stored record.
    /// </summary>
    /// <returns>The reconstructed command processing result.</returns>
    public CommandProcessingResult ToResult()
        => new(Accepted, ErrorMessage, CorrelationId);
}
