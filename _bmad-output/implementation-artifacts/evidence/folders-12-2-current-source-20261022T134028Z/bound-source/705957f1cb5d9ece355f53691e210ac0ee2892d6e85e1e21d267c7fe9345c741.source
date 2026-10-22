namespace Hexalith.EventStore.Server.Events;

/// <summary>
/// Result of publishing events to pub/sub.
/// </summary>
/// <param name="Success">Whether all events were published successfully.</param>
/// <param name="PublishedCount">Number of events successfully published.</param>
/// <param name="FailureReason">Failure reason if publication failed; null on success.</param>
public record EventPublishResult(
    bool Success,
    int PublishedCount,
    string? FailureReason);
