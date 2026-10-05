namespace Hexalith.EventStore.Server.Actors;

/// <summary>The pre-CausationId public record shape, retained solely as a compiled-consumer reference fixture.</summary>
/// <param name="CorrelationId">The command correlation identity.</param>
/// <param name="StartSequence">The first unpublished sequence.</param>
/// <param name="EndSequence">The last unpublished sequence.</param>
/// <param name="EventCount">The unpublished event count.</param>
/// <param name="CommandType">The original command type.</param>
/// <param name="IsRejection">Whether the committed events are rejections.</param>
/// <param name="FailedAt">The publication failure time.</param>
/// <param name="RetryCount">The drain retry count.</param>
/// <param name="LastFailureReason">The latest failure reason.</param>
/// <param name="MessageId">The original command message identity.</param>
/// <param name="DeadLettered">Whether dead-letter publication already completed.</param>
/// <param name="ReminderArmedAt">The last confirmed reminder registration time.</param>
public record UnpublishedEventsRecord(
    string CorrelationId,
    long StartSequence,
    long EndSequence,
    int EventCount,
    string CommandType,
    bool IsRejection,
    DateTimeOffset FailedAt,
    int RetryCount,
    string? LastFailureReason,
    string? MessageId = null,
    bool DeadLettered = false,
    DateTimeOffset? ReminderArmedAt = null);
