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
public record CommandStatusRecord(
    CommandStatus Status,
    DateTimeOffset Timestamp,
    string? AggregateId,
    int? EventCount,
    string? RejectionEventType,
    string? FailureReason,
    TimeSpan? TimeoutDuration);
