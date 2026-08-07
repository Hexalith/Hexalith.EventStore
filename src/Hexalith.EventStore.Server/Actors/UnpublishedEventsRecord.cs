namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Tracks unpublished events for drain recovery after publication failure.
/// Stored in actor state with key prefix "drain:" for automatic recovery
/// via DAPR actor reminders (Story 4.2).
/// </summary>
/// <param name="CorrelationId">The correlation ID of the failed command.</param>
/// <param name="StartSequence">First event sequence number in the unpublished range.</param>
/// <param name="EndSequence">Last event sequence number in the unpublished range.</param>
/// <param name="EventCount">Number of events in the range.</param>
/// <param name="CommandType">The command type that produced the events.</param>
/// <param name="IsRejection">Whether the events are rejection events.</param>
/// <param name="FailedAt">When the publication failure occurred.</param>
/// <param name="RetryCount">Number of drain retry attempts.</param>
/// <param name="LastFailureReason">Reason for the most recent drain failure.</param>
/// <param name="MessageId">The command message identifier, or <c>null</c> for a legacy correlation-keyed record.</param>
/// <param name="DeadLettered">
/// Story 4.4: whether the exhausted range has already been published to the dead-letter sink.
/// Persisted before the post-publish mutations so a fault between them cannot dead-letter the same
/// range twice. Trailing-optional so older persisted records still deserialize.
/// </param>
/// <param name="ReminderArmedAt">
/// Story 4.4: when publication recovery last confirmed a registered drain reminder for this record,
/// or <c>null</c> when no activation has confirmed one yet. Activation re-registers a reminder ONLY
/// while this is null: registering an already-armed reminder resets its due time, so a frequently
/// activated aggregate would postpone its own drain indefinitely. Trailing-optional so older
/// persisted records still deserialize.
/// </param>
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
    DateTimeOffset? ReminderArmedAt = null) {
    /// <summary>State key prefix for unpublished event records.</summary>
    public const string StateKeyPrefix = "drain:";

    /// <summary>Gets the actor state key for the command tracking identity.</summary>
    /// <param name="trackingId">The message ID for new records, or correlation ID for legacy records.</param>
    /// <returns>The state key in format "drain:{trackingId}".</returns>
    public static string GetStateKey(string trackingId) => $"{StateKeyPrefix}{trackingId}";

    /// <summary>Gets the DAPR actor reminder name for the command tracking identity.</summary>
    /// <param name="trackingId">The message ID for new records, or correlation ID for legacy records.</param>
    /// <returns>The reminder name in format "drain-unpublished-{trackingId}".</returns>
    public static string GetReminderName(string trackingId) => $"drain-unpublished-{trackingId}";

    /// <summary>Returns a new record with incremented retry count and updated failure reason.</summary>
    /// <param name="failureReason">The reason for the latest failure.</param>
    /// <returns>A new record with RetryCount + 1 and updated LastFailureReason.</returns>
    public UnpublishedEventsRecord IncrementRetry(string? failureReason) => this with {
        RetryCount = RetryCount + 1,
        LastFailureReason = failureReason,
    };

    /// <summary>Returns a new record durably marked as already published to the dead-letter sink.</summary>
    /// <param name="failureReason">The stable reason code recorded with the dead-letter.</param>
    /// <returns>A new record with <see cref="DeadLettered"/> set.</returns>
    public UnpublishedEventsRecord MarkDeadLettered(string? failureReason) => this with {
        DeadLettered = true,
        LastFailureReason = failureReason,
    };

    /// <summary>Returns a new record stamped with the moment its drain reminder was confirmed.</summary>
    /// <param name="armedAt">When the reminder registration succeeded.</param>
    /// <returns>A new record with <see cref="ReminderArmedAt"/> set.</returns>
    public UnpublishedEventsRecord MarkReminderArmed(DateTimeOffset armedAt) => this with {
        ReminderArmedAt = armedAt,
    };

    /// <summary>Gets the identifier used to key idempotency and status for this record.</summary>
    /// <param name="trackingId">The drain tracking identifier the reminder fired with.</param>
    /// <returns>The record's message id when present, otherwise the tracking id.</returns>
    public string GetTrackingIdentity(string trackingId)
        => string.IsNullOrWhiteSpace(MessageId) ? trackingId : MessageId;
}
