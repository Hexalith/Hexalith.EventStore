namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Records the status of a command with terminal-state-specific fields.
/// Non-terminal states have null values for terminal-specific fields.
/// </summary>
/// <param name="Status">The current command lifecycle status.</param>
/// <param name="Timestamp">When this status was recorded.</param>
/// <param name="AggregateId">The aggregate identifier (nullable for non-terminal states).</param>
/// <param name="EventCount">Number of events produced (Completed status only).</param>
/// <param name="RejectionEventType">Fully qualified rejection event type name for domain rejections.</param>
/// <param name="FailureReason">Description of an infrastructure or publication failure when available.</param>
/// <param name="TimeoutDuration">Duration before timeout occurred (TimedOut status only).</param>
/// <param name="MessageId">The command message identifier, or <c>null</c> for a legacy record.</param>
/// <param name="CorrelationId">The tracing correlation identifier, or <c>null</c> for a legacy record.</param>
/// <param name="Retryable">
/// Story 4.4 recovery signal, a real tri-state:
/// <c>true</c> — a drain is armed and the platform will retry publication automatically;
/// <c>false</c> — terminal, no further automatic attempt will be made (attempts exhausted,
/// dead-lettered, or no reminder armed);
/// <c>null</c> — a legacy record written before this field existed. <c>null</c> never means
/// "permanently failed".
/// </param>
/// <param name="RecoveryReasonCode">
/// The stable bounded reason code explaining the current recovery disposition (for example
/// <c>drain_attempts_exhausted</c>), or <c>null</c> when there is nothing to explain.
/// </param>
/// <param name="DrainAttemptCount">The number of drain attempts made so far, or <c>null</c> when no drain ran.</param>
public record CommandStatusRecord(
    CommandStatus Status,
    DateTimeOffset Timestamp,
    string? AggregateId,
    int? EventCount,
    string? RejectionEventType,
    string? FailureReason,
    TimeSpan? TimeoutDuration,
    string? MessageId = null,
    string? CorrelationId = null,
    bool? Retryable = null,
    string? RecoveryReasonCode = null,
    int? DrainAttemptCount = null);
