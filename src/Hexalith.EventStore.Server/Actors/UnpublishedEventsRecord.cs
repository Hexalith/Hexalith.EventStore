namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Tracks unpublished events for drain recovery after publication failure.
/// Stored in actor state with key prefix "drain:" for automatic recovery
/// via DAPR actor reminders (Story 4.4).
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
public record UnpublishedEventsRecord(
    string CorrelationId,
    long StartSequence,
    long EndSequence,
    int EventCount,
    string CommandType,
    bool IsRejection,
    DateTimeOffset FailedAt,
    int RetryCount,
    string? LastFailureReason)
{
    /// <summary>State key prefix for unpublished event records.</summary>
    public const string StateKeyPrefix = "drain:";

    /// <summary>Gets the actor state key for the given correlation ID.</summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>The state key in format "drain:{correlationId}".</returns>
    public static string GetStateKey(string correlationId) => $"{StateKeyPrefix}{correlationId}";

    /// <summary>Gets the DAPR actor reminder name for the given correlation ID.</summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>The reminder name in format "drain-unpublished-{correlationId}".</returns>
    public static string GetReminderName(string correlationId) => $"drain-unpublished-{correlationId}";

    /// <summary>Returns a new record with incremented retry count and updated failure reason.</summary>
    /// <param name="failureReason">The reason for the latest failure.</param>
    /// <returns>A new record with RetryCount + 1 and updated LastFailureReason.</returns>
    public UnpublishedEventsRecord IncrementRetry(string? failureReason) => this with
    {
        RetryCount = RetryCount + 1,
        LastFailureReason = failureReason,
    };
}
