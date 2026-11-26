namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>Result of observing or recording poison evidence.</summary>
public enum EventStoreSubscriberPoisonOutcome
{
    /// <summary>No durable retained record was confirmed yet.</summary>
    Retryable = 0,
    /// <summary>The exact scoped delivery was confirmed in the durable actor item and index.</summary>
    Retained = 1,
    /// <summary>An existing retained record conflicts with the attempted content or scope.</summary>
    Conflict = 2,
}
