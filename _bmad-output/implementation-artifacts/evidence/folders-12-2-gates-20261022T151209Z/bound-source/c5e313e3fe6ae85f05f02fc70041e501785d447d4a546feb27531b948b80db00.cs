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
/// <param name="MessageId">The command message identifier, or <c>null</c> for a legacy record.</param>
/// <param name="CommandType">The command type, or <c>null</c> for a legacy record.</param>
/// <param name="ExpiresAt">The application-visible expiration time, or <c>null</c> for a legacy record.</param>
/// <param name="Disposition">The processing disposition, or <c>null</c> for a legacy record.</param>
/// <param name="RejectionEventType">The stable domain rejection event type.</param>
/// <param name="FailureReason">The stable deterministic failure classification.</param>
/// <param name="ResultPayloadWithheld">Whether public result-payload projection was withheld.</param>
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
    int? BackpressureThreshold = null,
    string? MessageId = null,
    string? CommandType = null,
    DateTimeOffset? ExpiresAt = null,
    IdempotencyRecordDisposition? Disposition = null,
    string? RejectionEventType = null,
    string? FailureReason = null,
    bool ResultPayloadWithheld = false)
{
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
    /// Creates a self-describing <see cref="IdempotencyRecord"/> from a command result.
    /// </summary>
    /// <param name="identity">The exact command identity.</param>
    /// <param name="result">The command processing result.</param>
    /// <param name="processedAt">When the command result was produced.</param>
    /// <param name="expiresAt">When the record expires at the application layer.</param>
    /// <param name="disposition">Whether the record is terminal or recoverable.</param>
    /// <returns>A new self-describing idempotency record.</returns>
    public static IdempotencyRecord FromResult(
        CommandProcessingIdentity identity,
        CommandProcessingResult result,
        DateTimeOffset processedAt,
        DateTimeOffset expiresAt,
        IdempotencyRecordDisposition disposition)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        ArgumentNullException.ThrowIfNull(result);
        if (expiresAt <= processedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiration must be later than processing time.");
        }

        return new IdempotencyRecord(
            identity.CausationId,
            result.CorrelationId,
            result.Accepted,
            result.ErrorMessage,
            processedAt,
            result.EventCount,
            result.ResultPayload,
            result.BackpressureExceeded,
            result.BackpressurePendingCount,
            result.BackpressureThreshold,
            identity.MessageId,
            identity.CommandType,
            expiresAt,
            disposition,
            result.RejectionEventType,
            result.FailureReason,
            result.ResultPayloadWithheld);
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
            BackpressureThreshold,
            RejectionEventType,
            FailureReason,
            ResultPayloadWithheld);
}
