using Hexalith.EventStore.Client.Subscriptions;

namespace Hexalith.EventStore.Operations.Models;

/// <summary>Durable, bounded failure evidence for a subscriber delivery.</summary>
/// <param name="Fingerprint">The content and subscription fingerprint.</param>
/// <param name="Attempts">The number of observed failures.</param>
/// <param name="FirstFailure">When the first failure was persisted.</param>
/// <param name="LastFailure">When the latest failure was persisted.</param>
/// <param name="Reason">The latest bounded failure reason.</param>
/// <param name="Retryable">Whether normal handling may recover after correction.</param>
public sealed record SubscriberFailureEvidence(string Fingerprint, int Attempts, DateTimeOffset FirstFailure,
    DateTimeOffset LastFailure, EventStoreSubscriberFailureReason Reason, bool Retryable)
{
    /// <summary>Gets the bounded identities already counted in this delivery budget.</summary>
    public IReadOnlyList<Guid> ObservationIds { get; init; } = [];
}
