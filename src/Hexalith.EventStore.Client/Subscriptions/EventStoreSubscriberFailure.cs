namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Evidence to record for one failed delivery.</summary>
/// <param name="Reason">The bounded failure classification.</param>
/// <param name="Retryable">Whether the rejection may recover without an operator.</param>
/// <param name="AttemptLimit">The configured live delivery attempt limit.</param>
public sealed record EventStoreSubscriberFailure(EventStoreSubscriberFailureReason Reason, bool Retryable, int AttemptLimit)
{
    /// <summary>Gets the identity of this delivery observation across retries of the sink call.</summary>
    public Guid ObservationId { get; init; } = Guid.NewGuid();
}
